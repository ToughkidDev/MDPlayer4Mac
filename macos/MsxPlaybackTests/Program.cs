using MDPlayer;
using MDPlayer.Driver.MUCOM;
using System.Text;
using System.Text.Json;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
int assertions=0;
void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
string fixtures=args.Length>0?args[0]:Path.Combine(AppContext.BaseDirectory,"../../../../EngineSmokeTest/testdata/msx");
var corpus=Directory.GetFiles(fixtures,"*.mub");
foreach(string path in corpus)
{
    byte[] data=File.ReadAllBytes(path);
    var session=MusicEngine.Load(data,path);
    Check(session!=null,"Load failed: "+path);
    short[] samples=new short[4096];long energy=0;int peak=0;
    for(int i=0;i<1200 && !session.Driver.Stopped;i++)
    {
        int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
        foreach(short s in samples.Take(n)){energy+=Math.Abs((int)s);peak=Math.Max(peak,Math.Abs((int)s));}
    }
    Check(session.Driver.Stopped,"Finite song did not stop: "+path);
    if(session.Driver is MucomDotNET driver)Check(string.IsNullOrEmpty(driver.PlaybackError),path+": "+driver.PlaybackError);
    Check(energy>0,"Silent: "+path);
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
    short[] samples=new short[4096];long energy=0;
    for(int i=0;i<1200 && !session.Driver.Stopped;i++)
    {
        int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
        foreach(short s in samples.Take(n))energy+=Math.Abs((int)s);
    }
    Check(session.Driver.Stopped,"MUC did not end: "+path);
    if(session.Driver is MucomDotNET d2)Check(string.IsNullOrEmpty(d2.PlaybackError),path+": "+d2.PlaybackError);
    Check(energy>0,"MUC silent: "+path);
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
Console.WriteLine($"PASS total assertions={assertions}, MUB files={corpus.Length}");
