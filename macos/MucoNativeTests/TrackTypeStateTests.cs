using System.Text.Json;
using MDPlayer.MucoNative;

internal static class TrackTypeStateTests
{
    private sealed record State(byte[] W,byte[] M);
    private static List<State> Render(string route,string text,bool pn6=true)
    {
        string wave="?WD0={"+string.Join(',',Enumerable.Repeat(64,32))+"}\n";
        var score=MsxMusicalCompiler.Parse(wave+route+" ?XT1 "+(pn6?"?XH0 ":"")+"?XU1 ?EP- v12 o4 "+text+" !\n",_=>null);
        var player=new PackedMusicPlayer(new(score.Object),_=>{},_=>{});var states=new List<State>();
        int index=route switch{"MSXD"=>0,"MSXE"=>1,"MSXF"=>2,"SCCA"=>3,"SCCB"=>4,"SCCC"=>5,"SCCD"=>6,"SCCE"=>7,_=>throw new Exception(route)};
        while(!player.Ended&&states.Count<32)
        {
            player.Tick();using var json=JsonDocument.Parse(JsonSerializer.Serialize(player.Snapshot()));var c=json.RootElement[index];
            states.Add(new(c.GetProperty("w").GetBytesFromBase64(),c.GetProperty("m").GetBytesFromBase64()));
        }
        return states;
    }
    private static int Period(State s)=>s.W[10]|s.W[11]<<8;
    internal static void Run(Action<bool,string> check)
    {
        foreach(string route in new[]{"MSXD","MSXE","MSXF","SCCA","SCCB","SCCC","SCCD","SCCE"})
        {
            var clean=Render(route,"c%16");
            foreach(int mode in new[]{1,2})
            {
                // Cancellation in rhythm mode must affect the live note state;
                // returning to melody must never resurrect an old effect.
                var cancelled=Render(route,$"?XT1,{mode} ?MV0,$64 ?MW3 ?DB1 ?XT4,{mode} ?MV- ?DR ?XT1,{mode} c%16");
                check(cancelled.Select(Period).SequenceEqual(clean.Select(Period)),$"{route}/XT,{mode}: returning from rhythm resurrected cancelled vibrato/detune.");
                check(cancelled.All(s=>(s.W[14]&6)==0),$"{route}/XT,{mode}: cancelled pitch flags remained enabled.");
            }
            // A literal switch preserves dormant phases until explicitly
            // cancelled. This includes both an already sounding note and
            // its following duration boundary.
            const string effects="?MV0,$21 ?MW3 ?DB1 ";
            var steady=Render(route,effects+"c%8 c%8");
            var literalKeep=Render(route,effects+"c%8 ?XT4,2 ?XT1,2 c%8");
            check(literalKeep.Select(Period).SequenceEqual(steady.Select(Period)),route+": literal round-trip changed a retained effect.");
            // Legacy type changes retain their historical saved-state rule.
            // Keep this distinct from the direct PN6 APIs above.
            var legacy=Render(route,"?XT1 ?MV0,$64 ?MW3 ?DB1 ?XT4 ?MV- ?DR ?XT1 c%16");
            check(Period(legacy[0])==Period(clean[0])+1,route+": legacy type no longer restores its saved byte detune.");
            check(legacy.Take(16).Select(Period).Distinct().Count()>1,route+": legacy type no longer restores its saved vibrato.");
            var legacyPn1=Render(route,"?XT1 ?MV0,$64 ?MW3 ?DB1 ?XT4 ?MV- ?DR ?XT1 c%16",false);
            check(legacyPn1.Select(Period).SequenceEqual(clean.Select(Period)),route+": PN1 rhythm transition no longer clears note flags.");
        }
        Console.WriteLine("PASS direct/literal track transitions cancel or preserve live pitch effects; legacy restore retained on PSG/SCC five channels");
    }
}
