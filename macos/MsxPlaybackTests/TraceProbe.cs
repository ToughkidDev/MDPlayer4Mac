using System.Reflection;
using System.Text.Json;
using MDPlayer;
using musicDriverInterface;
using mucomDotNET.Driver;

internal static class TraceProbe
{
 internal static int Run(string input,string output,int limit,bool detailed=false)
 {
  byte[] source=File.ReadAllBytes(input);
  if(Path.GetExtension(input).Equals(".muc",StringComparison.OrdinalIgnoreCase))
  {
   source=MDPlayer.Driver.MUCOM.MucomDotNET.CompileMuc(source,input,out string error)??throw new Exception(error);
   File.WriteAllBytes(output+".compiled.mub",source);
  }
  var session=MusicEngine.Load(source,Path.ChangeExtension(input,".MUB"))??throw new Exception("MusicEngine.Load failed");
  const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var inner=session.Driver.GetType().GetField("driver",flags)!.GetValue(session.Driver)!;
  var work=(Work)inner.GetType().GetField("work",flags)!.GetValue(inner)!;
  var music=(Music2)inner.GetType().GetField("music2",flags)!.GetValue(inner)!;
  work.soundWork.GetType().GetProperty("RANDUM")!.SetValue(work.soundWork,(ushort)0x1993);
  using var trace=new BinaryWriter(File.Create(output+".trace"));
  using var detail=new StreamWriter(output+".rhythm.txt");
  int tick=-1,writes=0;
  inner.GetType().GetField("WriteOPNAP",flags)!.SetValue(inner,(Action<ChipDatum>)(d=>{
   if(d.port>=0&&d.address>=0&&d.data>=0){trace.Write(tick);trace.Write((byte)d.port);trace.Write((byte)d.address);trace.Write((byte)d.data);writes++;}
   if(detailed&&d.port==0&&d.address==16)detail.WriteLine($"{tick} value={d.data:X2} part={work.soundWork.currentCh} page={work.pg.pageNo} inst={work.pg.instrumentNumber} RHYTHM={work.soundWork.RHYTHM} q={work.pg.quantize} next={work.pg.dataAddressWork} stack={Environment.StackTrace}");
  }));
  while(work.Status>0&&tick<limit&&work.nowLoopCounter<2){tick++;music.PL_SND();}
  File.WriteAllText(output+".json",JsonSerializer.Serialize(new{input,tick,writes,work.Status,work.abnormalEnd,work.nowLoopCounter}));
  return work.Status<0||work.abnormalEnd?1:0;
 }
}
