using MDPlayer.Driver.MUCOM;
using musicDriverInterface;

namespace MDPlayer.MucoNative;

/// <summary>
/// A private musical sequencer for one playback session. Call AdvanceSample at
/// the fixed rendering rate; delivery order, including repeated key/shape/wave
/// writes, is preserved. The MDPlayer adapter only supplies sinks and a clock.
/// Instances do not share chip state or an MSX machine's memory/mapper state.
/// </summary>
public sealed class NativeMucomSession : IDisposable
{
    private readonly Action<NativeChipWrite> write;
    private readonly Action<NativeChip, byte[], int> pcm;
    private readonly MsxSoundMapper mapper;
    private bool disposed;
    private readonly PackedMusicPlayer packed;
    private readonly int renderingRate;
    private long packedElapsed;
    private int packedThreshold;
    public MsxMubProfile Profile { get; }
    public string Error { get; private set; } = "";
    public long Samples { get; private set; }
    public int Status => disposed ? 0 : packed!=null ? (packed.Ended && (!Profile.HasOpnaSequence || Sequencer.GetStatus()<1)?0:1) : Sequencer.GetStatus();
    public long LoopCounter => packed!=null ? (Profile.HasOpnaSequence && Sequencer.GetStatus()>0?Math.Min(packed.LoopCounter,Math.Max(0,Sequencer.GetNowLoopCounter())):packed.LoopCounter) : Math.Max(0, Sequencer.GetNowLoopCounter());
    // The compatibility driver is managed C#. It is not an MSX CPU host.
    // Kept internal for the existing reference-trace harness during migration.
    internal mucomDotNET.Driver.Driver Sequencer { get; }

    public NativeMucomSession(byte[] data, int sampleRate, Action<NativeChipWrite> write,
        Func<string, Stream> asset = null, Action<NativeChip, byte[], int> pcm = null, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(write);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        this.write = write; this.pcm = pcm;renderingRate=sampleRate;
        Profile = MsxMubProfile.Parse(data);
        mapper = new MsxSoundMapper(
            (r,v) => Emit(NativeChip.Opll,0,r,v),
            (r,v) => Emit(NativeChip.Psg,0,r,v),
            (r,v) => Emit(NativeChip.Opna1,0,r,v),
            (p,r,v) => Emit(NativeChip.Scc,p,r,v));
        Sequencer = new mucomDotNET.Driver.Driver();
        try
        {
            var actions = new List<ChipAction>();
            foreach (NativeChip chip in new[] {NativeChip.Opna1,NativeChip.Opna2,NativeChip.Opnb1,NativeChip.Opnb2,NativeChip.Opm})
            {
                NativeChip target = chip;
                actions.Add(new ChipSink(d => Route(target,d), (bytes,type) => this.pcm?.Invoke(target,bytes,type)));
            }
            Sequencer.Init(actions, Profile.SequencerData.Select(b => new MmlDatum(b)).ToArray(),
                name => asset?.Invoke(name), new object[] {false,true,false,sourcePath ?? ""});
            Sequencer.StartRendering(sampleRate, new[] {
                Tuple.Create("",7_987_200), Tuple.Create("",7_987_200),
                Tuple.Create("",8_000_000), Tuple.Create("",8_000_000),Tuple.Create("",3_579_545)});
            Sequencer.ConfigureMsx(Profile.IsPacked?(byte)0:Profile.Capabilities,mapper.Upload,Profile.RawFlags.HasValue || Profile.IsPacked,mapper.WriteSccNative);
            if(Profile.IsPacked)
            {
                packedThreshold=mucomDotNET.Driver.MsxRawTimer.TickSamples(Profile.PackedTimer);
                packed=new PackedMusicPlayer(Profile.Packed,write,b=>{packedThreshold=mucomDotNET.Driver.MsxRawTimer.TickSamples(b);if(Profile.HasOpnaSequence)Sequencer.SetMsxTimerB(b);});
            }
            else mapper.Start(Profile.NativeGroup,Profile.SccSsg,Profile.RawFlags,Profile.DedicatedScc);
            Sequencer.MusicSTART(0);
        }
        catch { Silence(); disposed=true; throw; }
    }

    private void Emit(NativeChip chip, int port, int address, int value)
        => write(new(chip,port,address,value));

    private void Route(NativeChip chip, ChipDatum d)
    {
        if(d == null || d.port<0 || d.address<0 || d.data<0) return;
        if(Profile.IsPacked)
        {
            if(chip==NativeChip.Opna1 && Profile.HasOpnaSequence){if(d.port==0 && d.address==0x26)packedThreshold=mucomDotNET.Driver.MsxRawTimer.TickSamples(d.data);Emit(chip,d.port,d.address,d.data);}
            return;
        }
        if(chip==NativeChip.Opna1 && Profile.SccSsg && d.port==0 && d.address<14)
            mapper.WriteSsg(d.address,d.data);
        else if(chip==NativeChip.Opna2)
        {
            if(!Profile.NativeGroup) return;
            if(d.port==0 && d.address==191 && (d.data&4)!=0
                && (!Profile.UsesScc || ((d.data&8)!=0)!=Profile.UsesSccPlus))
                throw new FormatException("Raw SCC target differs from the declared transport profile.");
            mapper.WriteNative(d.port,d.address,d.data);
        }
        else Emit(chip,d.port,d.address,d.data);
    }

    public void AdvanceSample()
    {
        if(disposed) return;
        try
        {
            long oldInterval=(long)packedThreshold*renderingRate;
            bool due=false;
            if(packed!=null && !packed.Ended){packedElapsed+=44100;due=packedElapsed>=oldInterval;if(due)packedElapsed-=oldInterval;}
            if(packed==null || Profile.HasOpnaSequence && Sequencer.GetStatus()>0)Sequencer.Rendering();
            if(due)
            {
                packed.Tick();
                while(packedElapsed>=(long)packedThreshold*renderingRate && !packed.Ended)
                {packedElapsed-=(long)packedThreshold*renderingRate;packed.Tick();}
            }
            Samples++;
            if(Status<1) Dispose();
        }
        catch(Exception ex) { Error=ex.Message; Dispose(); throw; }
    }

    private void Silence()
    {
        for(int ch=0;ch<6;ch++) Emit(NativeChip.Opna1,0,0x28,ch<3?ch:ch+1);
        Emit(NativeChip.Opna1,1,0,1); Emit(NativeChip.Opna1,0,0x10,0xbf);
        Emit(NativeChip.Opna1,0,7,0x3f);
        for(int ch=0;ch<3;ch++) Emit(NativeChip.Opna1,0,8+ch,0);
        if(packed!=null)packed.Stop();
        else if(Profile.NativeGroup) mapper.StopNative();
        if(packed==null && Profile.UsesScc) mapper.StopScc();
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        Silence();
    }

    private sealed class ChipSink : ChipAction
    {
        private readonly Action<ChipDatum> register;
        private readonly Action<byte[],int> pcm;
        internal ChipSink(Action<ChipDatum> register,Action<byte[],int> pcm) => (this.register,this.pcm)=(register,pcm);
        public override string GetChipName()=>"MDPlayer.MucoNative";
        public override void WriteRegister(ChipDatum value)=>register(value);
        public override void WritePCMData(byte[] data,int type,int unused)=>pcm(data,type);
        public override void WaitSend(long elapsed,int size) { }
    }
}
