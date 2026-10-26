namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    private int waveGateSample=-1;
    private int waveFlags,waveShift,waveRate,waveCount,wavePhase,waveOwner,waveA,waveB;
    private readonly byte[] generated=new byte[32];
    private readonly Dictionary<Channel,int[]> phaseWaves=new();
    private readonly Dictionary<Channel,int> wavePhases=new();
    private static int PolicyBits(int flags)=>((flags&3)<<2)|((flags&4)<<4);
    private int ReadWave(ref int p){int id=score.Read(ref p);if(id>=score.Waves.Length)throw new FormatException("Undefined SCC effect wave.");return id;}
    private void SetWaveRoutes()
    {
        foreach(var v in channels.Take(8))v.M[28]&=250;
        if((waveFlags&1)==0)return;channels[waveOwner].M[28]|=1;
        if((waveFlags&8)!=0&&waveOwner<7)channels[waveOwner+1].M[28]|=4;
    }
    private void WaveControl(Channel c,int sub,ref int p)
    {
        if(c.Auxiliary)throw new FormatException("Global waveform program cannot be configured inside an independent effect.");
        switch(sub)
        {
            case 53:
                if(c.Index is <3 or >7||c.Standard)throw new FormatException("Wave gate requires compact SCC.");
                int target=score.Read(ref p);
                if(target==255){waveGateSample=-1;return;}
                if(target>=160||target/32!=c.Index-3||wavePolicy!=0)throw new FormatException("Invalid wave gate target or wave-sharing policy.");
                waveGateSample=target;return;
            case 45:
                if(c.Index<3||c.Index>7)throw new FormatException("Interpolation requires SCC.");c.M[28]&=253;c.M[5]&=247;
                waveA=ReadWave(ref p);waveB=ReadWave(ref p);waveShift=score.Read(ref p);waveRate=score.Read(ref p);int flags=score.Read(ref p);
                if(waveShift>15||flags>7)throw new FormatException("Invalid interpolation parameters.");waveFlags=PolicyBits(flags)|1;waveOwner=c.Index;SetWaveRoutes();return;
            case 46:int mask=score.Read(ref p),value=score.Read(ref p);if(mask>7||value>7)throw new FormatException("Invalid interpolation policy.");waveFlags=(waveFlags&~PolicyBits(mask))|PolicyBits(value&mask);SetWaveRoutes();return;
            case 47:waveFlags=0;SetWaveRoutes();return;
            case 48:
                if(c.Index<3||c.Index>7)throw new FormatException("Phase waveform selection requires SCC.");phaseWaves[c]=new[]{ReadWave(ref p),ReadWave(ref p),ReadWave(ref p),ReadWave(ref p)};c.M[28]|=2;return;
            case 49:c.M[28]&=253;return;
        }
    }
    private void PhaseWave(Channel c,int phase)
    {
        if(!phaseWaves.TryGetValue(c,out var voices))return;int seen=wavePhases.GetValueOrDefault(c),mask=2<<phase;if((seen&mask)!=0)return;wavePhases[c]=seen|mask;
        c.WaveId=voices[phase];c.Word(38,score.U16(26+2*c.WaveId));c.W[15]|=128;
    }
    private void WaveOnset(Channel c)
    {
        if(Bit(c.M[28],1)&&c.M[29]!=5){wavePhases[c]=0;PhaseWave(c,0);}
        if(!Bit(c.M[28],0)||c.Index!=waveOwner||(waveFlags&1)==0||(waveFlags&192)==192)return;
        waveFlags&=221;if((waveFlags&64)!=0)waveFlags|=128;wavePhase=waveShift;waveCount=waveRate;Array.Copy(score.Waves[waveA],generated,32);WaveDirty(c);
    }
    private void WaveDirty(Channel c){c.W[15]|=128;if((waveFlags&8)!=0&&c.Index<7)channels[c.Index+1].W[15]|=128;}
    private void TickWaveProgram(Channel c)
    {
        if(Bit(c.M[28],1)&&c.M[29]==3&&Bit(c.W[15],0))
        {
            if(c.M[17]!=0&&c.W[36]>=c.W[4])PhaseWave(c,3);
            else if(Bit(c.PreEnvelopeFlags,5))PhaseWave(c,2);
            else if(Bit(c.W[15],3)&&c.W[34]!=0)PhaseWave(c,1);
        }
        if(!Bit(c.M[28],0)||c.Index!=waveOwner||(waveFlags&3)!=1)return;
        waveCount=B(waveCount-1);if(waveCount!=0)return;waveCount=waveRate;
        if(waveShift==0){Array.Copy(score.Waves[waveA],generated,32);waveFlags|=2;}
        else if(waveShift==15){Array.Copy(score.Waves[waveB],generated,32);waveFlags|=2;}
        else
        {
            wavePhase++;
            if(wavePhase>=16)
            {
                if((waveFlags&4)==0){Array.Copy(score.Waves[waveB],generated,32);waveFlags|=2;WaveDirty(c);return;}
                wavePhase=waveShift;Array.Copy(score.Waves[(waveFlags&32)==0?waveB:waveA],generated,32);waveFlags^=32;
            }
            var target=score.Waves[(waveFlags&36)==36?waveA:waveB];
            for(int i=0;i<32;i++)
            {
                int from=(sbyte)generated[i],to=(sbyte)target[i];if(from==to)continue;int delta=B(Math.Max(1,Math.Abs(from-to)>>4)<<waveShift);generated[i]=B(from+(to>from?delta:-delta));
            }
        }
        WaveDirty(c);
    }
    private byte[] GeneratedWave(Channel c)=>(byte[])generated.Clone();
}
