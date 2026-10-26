using MDPlayer.MucoNative;
internal static class WaveGateTests
{
    internal static void Run(Action<bool,string> check)
    {
        string defs="?WD0={"+string.Join(',',Enumerable.Repeat(42,32))+"}\n?WD1={"+string.Join(',',Enumerable.Repeat(-64,32))+"}\n";
        defs+=string.Concat(Enumerable.Range(2,14).Select(i=>"?WD"+i+"={"+string.Join(',',Enumerable.Repeat(0,32))+"}\n"));
        (PackedMusicPlayer P,List<NativeChipWrite> E) Make(string source)
        {
            var o=MsxMusicalCompiler.Parse(defs+source,_=>null);var e=new List<NativeChipWrite>();
            var p=new PackedMusicPlayer(new(o.Object),e.Add,_=>{});e.Clear();return(p,e);
        }
        bool IsWave(NativeChipWrite w,int a,int v)=>w.Chip==NativeChip.Scc&&w.Port==4&&w.Address==a&&w.Value==v;
        var x=Make("SCCA ?XU1 ?WP0 ?XT1 v12 @0 c%2 @1 c%2 !\nSCCE ?XU1 ?WG15 ?XT1 v12 @0 e%8 !\n");
        x.P.Tick();
        var bus=x.E.Where(w=>w.Chip==NativeChip.Scc).ToList();
        int first=bus.FindIndex(w=>IsWave(w,143,0));
        check(first>=0&&first+33<bus.Count,"WG pre-upload sample write missing");
        check(Enumerable.Range(0,32).All(n=>IsWave(bus[first+1+n],n,42)),"WG did not retain raw static wave");
        check(IsWave(bus[first+33],143,17),"WG did not restore shared gate mask into target sample");
        check(bus.Count(w=>w.Port==3)==1,"WG touched the actual gate around static uploads");
        x.E.Clear();x.P.Tick();
        check(!x.E.Any(w=>w.Chip==NativeChip.Scc&&w.Port==4),"WG rewrote target without an upload");
        x.E.Clear();x.P.Tick();
        check(x.E.Any(w=>IsWave(w,143,17))&&x.E.Any(w=>IsWave(w,0,192)),"Another route's static upload did not update WG target");
        var off=Make("SCCA ?XU1 ?WP0 ?XT1 v12 @0 c%2 @1 c%2 !\nSCCE ?XU1 ?WG15 ?XT1 r%2 ?WG- r%4 !\n");
        off.P.Tick();off.E.Clear();off.P.Tick();off.E.Clear();off.P.Tick();
        check(!off.E.Any(w=>IsWave(w,143,0))&&off.E.Count(w=>w.Chip==NativeChip.Scc&&w.Port==3)>=2,"WG- did not restore normal upload gate behavior");
        var fresh=Make("SCCA ?XU1 ?WP0 ?XT1 v12 @0 c%4 !\n");fresh.P.Tick();
        check(!fresh.E.Any(w=>IsWave(w,143,0)),"WG leaked across player instances");
        var dynamic=Make("SCCA ?XU1 ?WP0 ?XT1 ?WM0,63 v12 c%8 !\nSCCE ?XU1 ?WG15 ?XT1 r%8 !\n");
        dynamic.P.Tick();dynamic.E.Clear();dynamic.P.Tick();
        check(!dynamic.E.Any(w=>w.Chip==NativeChip.Scc&&w.Port==4&&w.Address==143),"WG affected WM dynamic upload");
        foreach(string bad in new[]{"MSXD ?XU1 ?WG15 c !","MSXA ?XU1 ?WG15 c !","SCCA ?WG15 c !","SCCA ?XU1 ?WG32 c !","SCCA ?XU1 ?WG-1 c !","SCCA ?XU1 ?IM0={?WG15 !} c !"})
        {
            bool rejected=false;try{Make(bad+"\n");}catch(FormatException){rejected=true;}
            check(rejected,"Invalid WG scope/operand accepted: "+bad);
        }
        foreach(string bad in new[]{"SCCA ?XU1 ?WP1 ?WG0 c !","SCCA ?XU1 ?WG0 ?WP2 c !"})
        {
            bool rejected=false;try{var q=Make(bad+"\n");q.P.Tick();}catch(FormatException){rejected=true;}
            check(rejected,"Conflicting WG/WP policy accepted");
        }
        foreach(int sample in new[]{0,31})
        {
            var q=Make("SCCA ?XU1 ?WP0 ?XT1 v12 @0 c%2 !\nSCCE ?XU1 ?WG"+sample+" ?XT1 r%2 !\n");q.P.Tick();
            check(q.E.Any(w=>IsWave(w,128+sample,1)),"WG boundary sample failed");
        }
        Console.WriteLine("PASS WG static-upload order, shared gate, no-upload, disable, instance isolation, WM, bounds and scope");
    }
}
