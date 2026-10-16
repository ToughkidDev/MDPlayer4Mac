using MDPlayer.Driver.MUCOM;
using MDPlayer;
using musicDriverInterface;
using System.Reflection;
using System.Text;

internal static class CurrentCompatibilityTests
{
 private delegate byte? FlagReader(ReadOnlySpan<byte> page,int part);
 internal static void Run(Action<bool,string> check,string fixtures)
 {
  // Raw declarations must be found after every valid extension, but never
  // inside operands. Invoke the production bounded instruction reader.
  var type=typeof(MsxMubProfile).Assembly.GetType("MDPlayer.Driver.MUCOM.MsxRawTransport")!;
  var read=type.GetMethod("ReadFlags",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<FlagReader>();
  byte[] marker={255,247,0,0,191,21};
  byte[][] commands={
   new byte[]{255,248,0,1},new byte[]{255,249,0x30,0x37,8,0},
   new byte[]{255,250,255,247,0,0,191,21},
   new byte[]{255,252,0,1,2,1},new byte[]{255,251,255,0,1,2,3},
   new byte[]{255,251,1}.Concat(new byte[20]).ToArray(),
   new byte[]{247,255,247,0,0,191,21,0,0},new byte[]{255,255}
  };
  foreach(var prefix in commands)
  {
   check(read(prefix.Concat(marker).Append((byte)0).ToArray(),0)==21,"Raw marker missed after extension "+Convert.ToHexString(prefix));
   check(read(prefix.Append((byte)0).ToArray(),0)==null,"Raw marker found inside extension operands");
  }
  check(read(new byte[]{255,240,4,8}.Concat(marker).Append((byte)0).ToArray(),3)==21,"SSG automatic-pan operand desynchronized raw scan");
  check(read(new byte[]{255,246,255,128}.Concat(marker).Append((byte)0).ToArray(),3)==21,"SSG user-wave operand desynchronized raw scan");
  check(read(new byte[]{255,251,72}.Concat(marker).Append((byte)0).ToArray(),3)==21,"SSG extended pan mistaken for FM volume table");
  check(read(new byte[]{248,4,8}.Concat(marker).Append((byte)0).ToArray(),0)==21,"FM automatic-pan wait desynchronized raw scan");
  check(read(new byte[]{248,64,8}.Concat(marker).Append((byte)0).ToArray(),6)==21,"Rhythm automatic-pan wait desynchronized raw scan");
  check(read(new byte[]{244,7,15,4}.Concat(marker).Append((byte)0).ToArray(),0)==21,"FM TL-LFO operand desynchronized raw scan");
  check(read(new byte[]{244,7,0}.Concat(marker).Append((byte)0).ToArray(),0)==21,"FM pitch-LFO mode desynchronized raw scan");
  check(read(new byte[]{244,7,1}.Concat(marker).Append((byte)0).ToArray(),3)==21,"SSG tremolo mode desynchronized raw scan");
  List<int[]> writes=new();
  var mapper=new MsxSoundMapper((r,v)=>{},(r,v)=>{},(r,v)=>writes.Add(new[]{9,r,v}),(p,r,v)=>writes.Add(new[]{p,r,v}));
  byte[] wave=Enumerable.Range(0,32).Select(x=>(byte)(x*7)).ToArray();
  mapper.Upload(0,wave,255);
  check(writes.Count(x=>x[0]==0)==32,"SSG must use common SCC waveform port");
  check(!writes.Any(x=>x[0]==4),"SSG used SCC+-only waveform port");
  writes.Clear();mapper.Upload(0,wave,128);
  check(!writes.Any(x=>x[0]==0),"Repeated wave was uploaded again");
  check(writes.Any(x=>x[0]==2)&&writes.Any(x=>x[0]==9),"Cached wave suppressed gain/noise updates");
  wave[0]^=127;writes.Clear();mapper.Upload(0,wave,128);
  check(writes.Count(x=>x[0]==0)==32,"Caller mutation aliased cached waveform");
  writes.Clear();mapper.Upload(1,wave,255);
  check(writes.Count(x=>x[0]==0)==32,"Per-channel waveform cache aliased another channel");
  List<int> RhythmWrites(byte[] source,string path)
  {
   var session=MusicEngine.Load(source,path)??throw new Exception("Rhythm regression load failed");
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var inner=session.Driver.GetType().GetField("driver",flags)!.GetValue(session.Driver)!;
   var music=(mucomDotNET.Driver.Music2)inner.GetType().GetField("music2",flags)!.GetValue(inner)!;
   List<int> events=new();
   inner.GetType().GetField("WriteOPNAP",flags)!.SetValue(inner,(Action<ChipDatum>)(d=>{if(d.port==0&&d.address==16&&(d.data&63)!=0)events.Add(d.data);}));
   for(int tick=0;tick<20;tick++)music.PL_SND();
   return events;
  }
  string rawPath=Path.Combine(fixtures,"rhythm-raw-rest.mub");
  check(RhythmWrites(File.ReadAllBytes(rawPath),rawPath).SequenceEqual(new[]{2,8}),"Unselected G rest killed authored raw rhythm");
  byte[] explicitMask=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes("#driver mucomDotNET\nA t198 y16,2 r%32\nG0 @63 [r%1]32\n"),Path.Combine(fixtures,"explicit.muc"),out string error);
  check(explicitMask!=null,error);
  check(RhythmWrites(explicitMask,Path.Combine(fixtures,"explicit.mub")).Any(x=>(x&128)!=0),"Explicit @63 no longer releases selected drums");
  // Confirm the repaired toolchain's note-mask semantics without masking
  // direct authored y writes. Key-off must remain possible for muted drums.
  string mutePath=Path.Combine(fixtures,"rhythm-mute.mub");
  check(RhythmWrites(File.ReadAllBytes(mutePath),mutePath).Where(x=>(x&128)==0).SequenceEqual(new[]{60,60,60}),"Toolchain RhythmMute SB did not preserve/unmute the other four drums");
  string rawMutePath=Path.Combine(fixtures,"rhythm-mute-raw.mub");
  check(RhythmWrites(File.ReadAllBytes(rawMutePath),rawMutePath).SequenceEqual(new[]{63}),"RhythmMute must not rewrite authored raw y16 writes");
  for(int mask=0;mask<64;mask++)
  {
   string letters=new string("BSCHTR".Where((_,i)=>(mask&(1<<i))!=0).ToArray());
   string header="#driver mucomDotNET\n#OPNA1RhythmMute "+letters+"\nA t225 r%2\n";
   byte[] notes=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(header+"G0 @63 c%1 r%1\n"),Path.Combine(fixtures,"mask.muc"),out string maskError);
   check(notes!=null,maskError);
   var events=RhythmWrites(notes,Path.Combine(fixtures,"mask.mub"));
   check(events.Where(x=>(x&128)==0).SequenceEqual(mask==63?Array.Empty<int>():new[]{63^mask}),"Rhythm mute mask "+mask);
   check(events.Contains(191),"Rhythm mute blocked key-off for mask "+mask);
   byte[] raw=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(header+"G0 y16,63 r%1 y16,191 r%1\n"),Path.Combine(fixtures,"raw-mask.muc"),out string rawError);
   check(raw!=null,rawError);
   check(RhythmWrites(raw,Path.Combine(fixtures,"raw-mask.mub")).SequenceEqual(new[]{63,191}),"Raw rhythm register bypass/key-off changed for mask "+mask);
  }
  byte[] MusicData(byte[] packet)=>packet.AsSpan(BitConverter.ToInt32(packet,4),BitConverter.ToInt32(packet,8)).ToArray();
  for(int mask=0;mask<64;mask++)for(int pan=4;pan<=6;pan++)foreach(int? wait in new int?[]{null,1,2,255})
  {
   string tail=wait.HasValue?","+wait.Value:"";
   string source=$"#driver mucomDotNET\nG @{mask} l8 pm1 p{pan}{tail} c\n";
   string packed="#driver mucomDotNET\nG @"+mask+" l8 "+string.Join(" ",Enumerable.Range(0,6).Where(i=>(mask&(1<<i))!=0).Select(i=>$"p{pan*16+i},{wait??16}"))+" c\n";
   var a=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(source),Path.Combine(fixtures,"pan.muc"),out string ae);
   var b=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(packed),Path.Combine(fixtures,"packed.muc"),out string be);
   check(a!=null&&b!=null,ae+be);
   check(MusicData(a).SequenceEqual(MusicData(b)),"pm1 auto-pan must encode one wait per selected drum: "+source);
  }
 }
}
