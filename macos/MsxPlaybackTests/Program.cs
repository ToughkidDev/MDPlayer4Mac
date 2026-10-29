using MDPlayer;
using MDPlayer.Driver.MUCOM;
using System.Text;
using System.Text.Json;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
if(args.Length==4 && args[0]=="--trace"){Environment.ExitCode=TraceProbe.Run(args[1],args[2],int.Parse(args[3]));return;}
if(args.Length==3 && args[0]=="--features"){Environment.ExitCode=FeatureCompatibilityTests.Run(args[1],args[2]);return;}
if(args.Length==3 && args[0]=="--album"){Environment.ExitCode=AlbumPlaybackTests.Run(args[1],args[2]);return;}
int assertions=0;
void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
string fixtures=args.Length>0?args[0]:Path.Combine(AppContext.BaseDirectory,"../../../../EngineSmokeTest/testdata/msx");
if(args.Length>0 && args[0]=="--chip-usage")
{
    fixtures=args.Length>1?args[1]:Path.Combine(AppContext.BaseDirectory,"../../../../EngineSmokeTest/testdata/msx");
    MsxChipUsageTests.Run(Check,fixtures);
    Console.WriteLine($"CHIP USAGE PASS {assertions} assertions");
    return;
}
ModernPlaybackTests.Run(Check,fixtures);
Scc5Tests.Run(Check,fixtures);
MsxChipUsageTests.Run(Check,fixtures);
CurrentCompatibilityTests.Run(Check,fixtures);
var corpus=Directory.GetFiles(fixtures,"*.mub");
Dictionary<string,long> renderedFrames=new();
foreach(string path in corpus)
{
    byte[] data=File.ReadAllBytes(path);
    var session=MusicEngine.Load(data,path);
    Check(session!=null,"Load failed: "+path);
    if(session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.AY8910,out uint psgClock))
        Check(psgClock==1_789_773 / 2,"Native AY core clock differs from the VGM/MSX half-clock convention");
    short[] samples=new short[4096];long energy=0,frames=0;int peak=0;
    for(int i=0;i<1200 && !session.Driver.Stopped;i++)
    {
        int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
        frames+=n/2;
        foreach(short s in samples.Take(n)){energy+=Math.Abs((int)s);peak=Math.Max(peak,Math.Abs((int)s));}
    }
    Check(session.Driver.Stopped,"Finite song did not stop: "+path);
    if(session.Driver is MucomDotNET driver)Check(string.IsNullOrEmpty(driver.PlaybackError),path+": "+driver.PlaybackError);
    Check(energy>0,"Silent: "+path);
    renderedFrames[Path.GetFileNameWithoutExtension(path)]=frames;
    if(Path.GetFileName(path).StartsWith("native-raw2-"))
    {
        var profile=MsxMubProfile.Parse(data);
        Check(profile.RawFlags.HasValue,"Missing raw v2 chip declaration");
        Check(session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.K051649)==profile.UsesScc,"Raw SCC was not initialized correctly");
        Check(session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.YM2413emu)==profile.UsesOpll,"Raw v2 acquired an undeclared OPLL");
        Check(session.UsesK052539SccPlus==profile.UsesSccPlus,"SCC/SCC+ variant mismatch");
        if(profile.UsesScc)
        {
            var channels=session.ChipRegister.GetK051649Register(0).channel_list;
            Check(channels.All(c=>c.key==0 && c.volume==0),"Raw SCC stayed keyed after completion");
            int channel=profile.UsesSccPlus?4:0;
            Check(channels[channel].waveram.Take(16).All(n=>n==127) && channels[channel].waveram.Skip(16).All(n=>n==-128),"Raw SCC wave/address increment mismatch");
        }
    }
    Console.WriteLine($"PASS MUB {Path.GetFileName(path)} {session.DescribeActiveChips()} peak={peak}");
    if(MsxMubProfile.HasMagic(data))
    {
        Check(!session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.YM2610),"Unexpected OPNB");
        // Every truncation must be rejected before reaching the upstream allocator.
        int tag=BitConverter.ToInt32(data,18);int payloadEnd=tag>0?tag:data.Length;
        for(int n=0;n<payloadEnd;n++)
        {
            try{MsxMubProfile.Parse(data[..n]);throw new Exception("Accepted truncated msPb at "+n);}
            catch(FormatException){assertions++;}
        }
        for(int n=27;n<34 && data[7]=='1';n++)
        {
            byte[] bad=(byte[])data.Clone();bad[n]=1;
            Check(MusicEngine.Load(bad,path)==null,"Accepted reserved msPb byte "+n);
        }
    }
}
foreach(string path in Directory.GetFiles(fixtures,"*.muc"))
{
    var session=MusicEngine.Load(File.ReadAllBytes(path),path);
    if(session==null) { MucomDotNET.CompileMuc(File.ReadAllBytes(path),path,out string error); throw new Exception("MUC failed: "+path+" "+error); }
    short[] samples=new short[4096];long energy=0,frames=0;
    for(int i=0;i<1200 && !session.Driver.Stopped;i++)
    {
        int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
        frames+=n/2;
        foreach(short s in samples.Take(n))energy+=Math.Abs((int)s);
    }
    Check(session.Driver.Stopped,"MUC did not end: "+path);
    if(session.Driver is MucomDotNET d2)Check(string.IsNullOrEmpty(d2.PlaybackError),path+": "+d2.PlaybackError);
    Check(energy>0,"MUC silent: "+path);
    if(Path.GetFileName(path).Contains("default-psg") || Path.GetFileName(path).StartsWith("rhythm-mute"))
        Check(frames==renderedFrames[Path.GetFileNameWithoutExtension(path)],"MSX default tempo differs between MUC and toolchain MUB");
    Console.WriteLine("PASS MUC "+Path.GetFileName(path));
}
// Integer target mapper differential vectors come from executing the assembled MSX driver.
string vectors=Path.Combine(fixtures,"mapper-vectors.json");
if(File.Exists(vectors))foreach(var item in JsonDocument.Parse(File.ReadAllText(vectors)).RootElement.EnumerateArray())
{
    List<int[]> actual=new();
    var mapper=new MsxSoundMapper((r,v)=>actual.Add(new[]{0,r,v}),(r,v)=>actual.Add(new[]{1,r,v}),(r,v)=>actual.Add(new[]{2,r,v}),(p,r,v)=>actual.Add(new[]{3,p,r,v}));
    foreach(var command in item.GetProperty("writes").EnumerateArray())
    {
        int[] c=command.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        if(item.TryGetProperty("ssg",out var ssg) && ssg.GetBoolean()) mapper.WriteSsg(c[1],c[2]);
        else mapper.WriteNative(c[0],c[1],c[2]);
    }
    string got=JsonSerializer.Serialize(actual),expected=item.GetProperty("expected").GetRawText();
    int[][] wanted=JsonSerializer.Deserialize<int[][]>(expected)!;
    Check(actual.Count==wanted.Length && actual.Zip(wanted).All(pair=>pair.First.SequenceEqual(pair.Second)),"Mapper vector failed: "+item.GetProperty("name")+" actual="+got+" expected="+expected);
}
for(int n=0;n<10;n++)Check(mucomDotNET.Driver.Music2.MsxPreset(n).Length==32,"preset");
byte[] unknown=File.ReadAllBytes(corpus.First(x=>MsxMubProfile.HasMagic(File.ReadAllBytes(x))));
foreach(byte flag in new byte[]{0,1,4,255})
{
    var bad=(byte[])unknown.Clone();bad[7]=(byte)'1';bad[26]=flag;
    Check(MusicEngine.Load(bad,"invalid.mub")==null,"Accepted unknown flag "+flag);
}
// PCMInvert applies to K relative octaves, not sample data or ordinary parts.
byte[] Compile(string text)
{
    byte[] result=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(text),Path.Combine(fixtures,"inline.muc"),out string error);
    Check(result!=null,"Inline MUC failed: "+error);return result;
}
byte[] Body(byte[] data) => data.AsSpan(BitConverter.ToInt32(data,4),BitConverter.ToInt32(data,8)).ToArray();
Check(Body(Compile("#PCMInvert On\nK o4 c > d < e\n")).SequenceEqual(Body(Compile("K o4 c < d > e\n"))),"PCMInvert K relative octave");
Check(!Body(Compile("#PCMInvert On\nK o4 c > d < e\n")).SequenceEqual(Body(Compile("K o4 c > d < e\n"))),"PCMInvert K must change note stream");
Check(Body(Compile("#PCMInvert On\nD o4 c > d < e\n")).SequenceEqual(Body(Compile("D o4 c > d < e\n"))),"PCMInvert leaked to D");
foreach(string source in new[]{"#SSGExtend Wrong\nD c\n","#SSGExtend On\nMSXK c\n","D c\n#SSGExtend On\n","#SSGExtend On\nD @W256 c\n"})
{
    byte[] result=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(source),Path.Combine(fixtures,"invalid.muc"),out string error);
    Check(result==null && !string.IsNullOrEmpty(error),"Accepted invalid MUC: "+source);
}
Check(MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes("MSXA0B0 c\n#SSGExtend On\n"),Path.Combine(fixtures,"invalid.muc"),out string lateError)==null && lateError.Length>0,"Late extension tag accepted after interleaved selectors");
byte[] framed=new byte[]{0xef,0xbb,0xbf}.Concat(Encoding.ASCII.GetBytes("MSXD @0 v15 o4 c\n\x1aMSXK invalid\n")).ToArray();
byte[] framedOutput=MucomDotNET.CompileMuc(framed,Path.Combine(fixtures,"framed.muc"),out string framedError);
Check(framedOutput!=null && MsxMubProfile.HasMagic(framedOutput),"BOM/CP-M EOF lost MSX source profile: "+framedError);
// MUC sources are limited to 60,000 bytes (file size); one byte more is rejected with the size in the message.
byte[] SizedMuc(int size)
{
    const string body="D @0 v12 o4 c4\n";
    var text=new StringBuilder();
    while(text.Length+body.Length+100<=size)text.Append(';').Append('x',98).Append('\n');
    int rest=size-body.Length-text.Length;
    if(rest==1)text.Append('\n');else if(rest>=2)text.Append(';').Append('x',rest-2).Append('\n');
    byte[] bytes=Encoding.ASCII.GetBytes(text.Append(body).ToString());
    Check(bytes.Length==size,"Sized MUC fixture length");
    return bytes;
}
Check(MucomDotNET.CompileMuc(SizedMuc(MucomDotNET.MaxMucSourceBytes),Path.Combine(fixtures,"limit.muc"),out string limitError)!=null,"MUC at the 60,000-byte limit rejected: "+limitError);
Check(MucomDotNET.CompileMuc(SizedMuc(MucomDotNET.MaxMucSourceBytes+1),Path.Combine(fixtures,"oversize.muc"),out string oversizeError)==null
    && oversizeError.Contains("60,001") && oversizeError.Contains("60,000"),"MUC over 60,000 bytes accepted or unclear error: "+oversizeError);
Check(MusicEngine.Load(SizedMuc(MucomDotNET.MaxMucSourceBytes+1),Path.Combine(fixtures,"oversize.muc"))==null
    && MusicEngine.LastLoadError.Contains("60,000"),"MusicEngine did not report the MUC size limit");
// Exercise command truncation/unsupported preset through the renderer, beyond directory validation.
byte[] extended=File.ReadAllBytes(Path.Combine(fixtures,"scc-presets.mub"));
int waveCommand=-1;
for(int i=34;i<extended.Length-2;i++)if(extended[i]==255 && extended[i+1]==237)waveCommand=i;
Check(waveCommand>0,"Missing fixture ED command");
foreach(byte preset in new byte[]{10,254,255})
{
    byte[] bad=(byte[])extended.Clone();bad[waveCommand+2]=preset;
    var session=MusicEngine.Load(bad,"invalid-preset.mub");
    Check(session!=null,"Directory validation rejected command test");
    short[] samples=new short[4096];
    for(int i=0;i<100 && !session.Driver.Stopped;i++)session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
    Check(session.Driver.Stopped && session.Driver is MucomDotNET d && !string.IsNullOrEmpty(d.PlaybackError),"Invalid preset did not fail safely: "+preset);
}
List<int[]> stopWrites=new();
var silence=new MsxSoundMapper((r,v)=>stopWrites.Add(new[]{0,r,v}),(r,v)=>stopWrites.Add(new[]{1,r,v}),(r,v)=>{},(p,r,v)=>stopWrites.Add(new[]{3,p,r,v}));
silence.StopNative();silence.StopScc();
Check(stopWrites.Count(x=>x[0]==0 && x[1]>=0x20 && x[1]<=0x28 && x[2]==0)==9,"Native stop must release all raw OPLL channels");
Check(stopWrites.Any(x=>x.SequenceEqual(new[]{3,3,0,0})) && stopWrites.Count(x=>x[0]==3 && x[1]==2 && x[3]==0)==5,"SCC stop must disable all five channels");
// Invalid version markers fail before chip allocation; payload bytes that only
// resemble a marker must not select a raw target.
byte[] raw2=File.ReadAllBytes(Path.Combine(fixtures,"native-raw2-scc.mub"));
int marker=-1;
for(int i=34;i<raw2.Length-6;i++)if(raw2.AsSpan(i,5).SequenceEqual(new byte[]{255,247,0,0,191})){marker=i;break;}
Check(marker>=0,"Missing raw v2 marker");
foreach(byte flag in new byte[]{2,9,25,33,255})
{
    byte[] bad=(byte[])raw2.Clone();bad[marker+5]=flag;
    Check(MusicEngine.Load(bad,"invalid-raw2.mub")==null,"Accepted invalid raw flags "+flag);
}
byte[] operands=(byte[])raw2.Clone();
// The preceding FA command's operand and a valid F7 command form the byte
// sequence FF F7 00 00 BF 15. Its FF is not an instruction boundary.
operands[marker-3]=250;operands[marker-2]=0;operands[marker-1]=255;
new byte[]{247,0,0,191,21,0}.CopyTo(operands,marker);
Check(MsxMubProfile.Parse(operands).RawFlags==null,"Mistook register data for a raw marker");
List<int[]> carried=new();
var continuity=new MsxSoundMapper((r,v)=>{},(r,v)=>carried.Add(new[]{1,r,v}),(r,v)=>{},(p,r,v)=>carried.Add(new[]{3,p,r,v}));
continuity.WriteNative(0,191,29);continuity.WriteNative(0,146,128);
for(int lap=0;lap<3;lap++)
{
    continuity.WriteNative(0,191,29);
    continuity.WriteNative(0,148,17);continuity.WriteNative(0,148,33);
}
Check(carried.Where(w=>w[0]==3).Select(w=>w[2]).SequenceEqual(Enumerable.Range(128,6)),"Loop marker reset SCC address state");
continuity.WriteNative(0,191,17);continuity.WriteNative(0,148,44);
Check(carried.Last().SequenceEqual(new[]{3,4,134,44}),"Raw marker lost the existing SCC lease");
continuity.StopNative();continuity.StopScc();
Check(!continuity.RawScc,"SCC lease stayed active after stop");
string timingVectors=Path.Combine(fixtures,"tick-vectors.json");
if(File.Exists(timingVectors))
{
    using var timing=JsonDocument.Parse(File.ReadAllText(timingVectors));
    int[] thresholds=timing.RootElement.GetProperty("samples").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    Check(thresholds.Length==256,"Missing Work timer values");
    for(int value=0;value<256;value++)
    {
        int expected=thresholds[value];
        Check(mucomDotNET.Driver.MsxRawTimer.TickSamples(value)==expected,"Work timer threshold "+value);
        var timer=new mucomDotNET.Driver.MsxRawTimer(44100,7987200);
        timer.WriteReg(0x26,(byte)value);timer.WriteReg(0x27,0x0a);
        for(int sample=1;sample<=expected*3;sample++)
        {
            timer.timer();
            Check(((timer.StatReg&2)!=0)==(sample%expected==0),"Raw timer cadence "+value);
            if((timer.StatReg&2)!=0)timer.WriteReg(0x27,0x2a);
        }
    }
    // Changing tempo and clearing status must retain an overdue remainder.
    var phase=new mucomDotNET.Driver.MsxRawTimer(44100,7987200);
    phase.WriteReg(0x26,254);phase.WriteReg(0x27,0x0a);
    for(int i=0;i<20;i++)phase.timer();
    phase.WriteReg(0x26,255);phase.timer();
    Check((phase.StatReg&2)!=0,"Tempo change lost pending phase");
    phase.WriteReg(0x27,0x2a);
    for(int i=0;i<2;i++)phase.timer();
    Check(phase.StatReg==0,"Raw acknowledgement reset phase incorrectly");
    phase.timer();Check((phase.StatReg&2)!=0,"Raw phase did not survive acknowledgement");
}

if(args.Length>1)RealRaw2Tests.Run(args[1],Check);
Console.WriteLine($"PASS total assertions={assertions}, MUB files={corpus.Length}");
