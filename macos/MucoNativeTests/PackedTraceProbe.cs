using System.Text.Json;
using MDPlayer.MucoNative;
internal static class PackedTraceProbe
{
    internal static void Run(string input,string output,int ticks)
    {
        Directory.CreateDirectory(output);var results=new List<object>();
        foreach(string file in Directory.GetFiles(input,"*.obj").OrderBy(x=>x,StringComparer.Ordinal))
        {
            string id=Path.GetFileNameWithoutExtension(file);var events=new List<object>();int frame=-1;
            try
            {
                using var log=new StreamWriter(Path.Combine(output,id+".jsonl"));
                var player=new PackedMusicPlayer(new(File.ReadAllBytes(file)),e=>events.Add(new[]{(int)e.Chip,e.Port,e.Address,e.Value}),_=>{});
                log.WriteLine(JsonSerializer.Serialize(new{tick=-1,writes=events}));events.Clear();
                for(frame=0;frame<ticks&&!player.Ended;frame++){player.Tick();log.WriteLine(JsonSerializer.Serialize(new{tick=frame,writes=events,states=player.Snapshot()}));events.Clear();}
                results.Add(new{id,success=true,ticks=frame,ended=player.Ended,loops=player.LoopCounter});
            }
            catch(Exception e){results.Add(new{id,success=false,tick=frame,error=e.Message,kind=e.GetType().Name});}
        }
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Traced "+results.Count+" packed objects");
    }
}
