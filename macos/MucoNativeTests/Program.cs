using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MDPlayer.MucoNative;
using MDPlayer.Driver.MUCOM;

if(args.Length==1&&args[0]=="--wm-state") { int wmChecks=0; WmStateTests.Run((ok,message)=>{wmChecks++;if(!ok)throw new Exception(message);});Console.WriteLine("WM checks="+wmChecks); return; }
if(args.Length==4&&args[0]=="--trace-packed") { PackedTraceProbe.Run(args[1],args[2],int.Parse(args[3])); return; }
if(args.Length==3&&args[0]=="--compile-modern") { ModernCompilerProbe.Run(args[1],args[2]); return; }

// No MDPlayerCore/MDSound reference: this executable exercises the actual public
// native-core boundary with ordered register delivery and its own sample clock.
string fixtures=Path.GetFullPath(args.Length>0?args[0]:"macos/EngineSmokeTest/testdata/msx");
int checks=0;
void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
Stream Asset(string name)
{
    string path=Path.Combine(fixtures,name.Replace('\\',Path.DirectorySeparatorChar));
    return File.Exists(path)?File.OpenRead(path):null;
}
TrackTypeStateTests.Run(Check);
WmStateTests.Run(Check);
SourceControlTests.Run(Check);
WaveGateTests.Run(Check);
ToolchainSyncTests.Run(Check);
var names=typeof(NativeMucomSession).Assembly.GetReferencedAssemblies().Select(x=>x.Name).ToArray();
Check(!names.Any(x=>x is "MDPlayerCore" or "MDSound" or "Z80dotNet"),"Native core depends on a host or CPU emulator.");
var results=new List<object>();
foreach(string file in Directory.GetFiles(fixtures,"*.mub").OrderBy(x=>x,StringComparer.Ordinal))
{
    byte[] bytes=File.ReadAllBytes(file);
    if(!MsxMubProfile.HasMagic(bytes)) continue;
    var profile=MsxMubProfile.Parse(bytes);
    var writes=new List<NativeChipWrite>();
    using var session=new NativeMucomSession(bytes,44_100,writes.Add,Asset);
    while(session.Status>0 && session.Samples<10_000_000) session.AdvanceSample();
    Check(session.Status==0 && session.Error=="","Native playback did not finish: "+file);
    Check(writes.Count>0,"No chip output: "+file);
    if(profile.UsesScc) Check(writes.Last(x=>x.Chip==NativeChip.Scc&&x.Port==3).Value==0,"SCC gates remained active.");
    if(profile.UsesOpll) for(int channel=0;channel<9;channel++)
        Check((writes.Last(x=>x.Chip==NativeChip.Opll&&x.Address==0x20+channel).Value&16)==0,"OPLL channel remained keyed.");
    int count=writes.Count; session.Dispose();session.AdvanceSample();
    Check(writes.Count==count,"Repeated stop emitted duplicate output.");
    results.Add(new {file=Path.GetFileName(file),session.Samples,writes=count});
}
// An invalid load must not emit chip writes; the next session starts normally.
int badWrites=0;
try { using var bad=new NativeMucomSession(Encoding.ASCII.GetBytes("msPb0109"),44_100,_=>badWrites++);Check(false,"Truncated profile was accepted."); }
catch(FormatException) { Check(badWrites==0,"Malformed profile emitted output."); }
string Wave(int value)=>"@W0:{"+string.Join(',',Enumerable.Repeat(value,32))+"}\n";
byte[] Compile(string score)=>NativeMucCompiler.Compile(Encoding.ASCII.GetBytes(score),Asset);
byte[] a=Compile(Wave(90)+"SCCE C192t240q0 @0 E255,0,0,0,0,255 v15 o4 c%24 r%4\n");
byte[] b=Compile(Wave(-50)+"SCCE C192t240q0 @0 E255,0,0,0,0,255 v15 o4 d%24 r%4\n");
List<NativeChipWrite> Render(byte[] data)
{
    var events=new List<NativeChipWrite>();using var s=new NativeMucomSession(data,44_100,events.Add,Asset);
    while(s.Status>0 && s.Samples<1_000_000)s.AdvanceSample();
    Check(s.Status==0&&s.Error=="","Recovery playback failed.");return events;
}
var expectedA=Render(a);var expectedB=Render(b);
var simultaneousA=new List<NativeChipWrite>();var simultaneousB=new List<NativeChipWrite>();
using(var sa=new NativeMucomSession(a,44_100,simultaneousA.Add,Asset))
using(var sb=new NativeMucomSession(b,44_100,simultaneousB.Add,Asset))
{
    for(int i=0;i<1_000_000&&(sa.Status>0||sb.Status>0);i++) { sa.AdvanceSample();sb.AdvanceSample(); }
    Check(sa.Status==0&&sb.Status==0,"Independent sessions did not finish.");
}
Check(simultaneousA.SequenceEqual(expectedA)&&simultaneousB.SequenceEqual(expectedB),"Sessions shared wave/pitch state.");
Check(Render(a).SequenceEqual(expectedA),"Stop/restart retained previous song state.");
// Framing and public standalone compiler agree byte-for-byte, including OPNA.
foreach(string score in new[]{"D @15 o4 v12 c8\n","  A C192t240q0 o4 c%4 r%2\n",Wave(90)+"SCCE @0 o4 v15 c8\n"})
{
    byte[] plain=Encoding.ASCII.GetBytes(score);
    byte[] framed=new byte[]{0xef,0xbb,0xbf}.Concat(plain).Concat(new byte[]{0x1a,0xff}).ToArray();
    Check(NativeMucCompiler.Compile(plain,Asset).SequenceEqual(NativeMucCompiler.Compile(framed,Asset)),"BOM/EOF changed native compile output.");
}
try { Compile(Wave(0)+"SCCA @1 c\n");Check(false,"Invalid waveform accepted."); }
catch(FormatException) { Check(Render(a).SequenceEqual(expectedA),"Compiler error poisoned next compile/playback."); }
Console.WriteLine(JsonSerializer.Serialize(new{checks,files=results,dependencyNames=names},new JsonSerializerOptions{WriteIndented=true}));
