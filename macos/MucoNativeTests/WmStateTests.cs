using System.Text.Json;
using MDPlayer.MucoNative;

internal static class WmStateTests
{
    private sealed record State(byte[] W,byte[] M);
    internal static void Run(Action<bool,string> check)
    {
        string waves=string.Concat(Enumerable.Range(0,32).Select(i=>"?WD"+i+"={"+string.Join(',',Enumerable.Repeat(i==0?32:i==1?-64:i-16,32))+"}\n"));
        foreach(int version in new[]{2,3,4,5,6})foreach(int policy in new[]{0,1,2})foreach(string morph in new[]{"?WM0,63","@w0,63"})
        {
            string prefix=waves+(version==3?"MSXA ?XU1 ?XT1 @1 r%16 !\n":"")+"SCCA "+(version==5?"":"?XU1 ")+"?XT1 ?WP"+policy+" "+(version>=4?"?EP3,4,32,65 ":"?EP- ")+(version==6?"?XH0 ":"")+"v12 o4 ";
            (PackedMusicPlayer Player,List<NativeChipWrite> Events) Create(string text)
            {
                var score=MsxMusicalCompiler.Parse(text,_=>null);check(score.Object[2]=='0'+version,"Unexpected test profile.");
                var events=new List<NativeChipWrite>();return(new(new(score.Object),events.Add,_=>{}),events);
            }
            State Next(PackedMusicPlayer player)
            {
                player.Tick();using var json=JsonDocument.Parse(JsonSerializer.Serialize(player.Snapshot()));var s=json.RootElement[3];return new(s.GetProperty("w").GetBytesFromBase64(),s.GetProperty("m").GetBytesFromBase64());
            }
            string label=$"PN{version}/WP{policy}/{morph}";
            var reset=Create(prefix+morph+" r%8 "+morph+" r%4 !\n");
            State state=Next(reset.Player);check(state.M[8]==0&&state.M[10]==0&&(state.M[18]&64)==0,label+": configuration did not start at zero.");
            check((state.M[25]&1)!=0,label+": rest lost the configured zero-phase marker.");
            state=Next(reset.Player);check(state.M[8]==63,label+": rest restarted/paused the accumulator.");
            for(int i=2;i<=5;i++)state=Next(reset.Player);
            check(state.M[8]==59&&(state.M[18]&64)!=0,label+": carry did not persist during the rest.");
            for(int i=6;i<=8;i++)state=Next(reset.Player);
            check(state.M[8]==0&&state.M[10]==0&&(state.M[18]&64)==0,label+": reconfiguration did not clear accumulated phase/carry.");
            state=Next(reset.Player);check(state.M[8]==63,label+": post-reset rest incorrectly took the pitched-onset path.");
            var selected=Create(prefix+morph+" c%4 @1 r%8 !\n");
            var control=Create(prefix+morph+" c%4 r%8 !\n");
            State chosen=null,baseline=null;
            for(int tick=0;tick<4;tick++){chosen=Next(selected.Player);baseline=Next(control.Player);}
            selected.Events.Clear();chosen=Next(selected.Player);baseline=Next(control.Player);
            check((chosen.M[5]&8)==0&&(chosen.M[5]&64)!=0,label+": static voice discarded morph release ownership.");
            foreach(int at in new[]{10,11,12,13,14,18,21,26,27,28,29,30,31,32,33,34,35,36})
                check(chosen.W[at]==baseline.W[at],label+": static selection changed envelope/gate state "+at);
            check(selected.Events.Last(x=>x.Chip==NativeChip.Scc&&x.Port==3).Value==(version==5?0:1),label+": static selection lost the gate under this WP policy.");
            check(selected.Events.Where(x=>x.Chip==NativeChip.Scc&&x.Port==4&&x.Address<32).Select(x=>x.Value).SequenceEqual(Enumerable.Repeat(192,32)),label+": static wave was not uploaded.");
            for(int tick=5;tick<10;tick++)
            {
                selected.Events.Clear();chosen=Next(selected.Player);baseline=Next(control.Player);
                check(chosen.W[12]==baseline.W[12]&&chosen.W[31]==baseline.W[31]&&chosen.W[32]==baseline.W[32]&&chosen.M[31]==baseline.M[31],label+": static selection reset envelope/release progression.");
                check(!selected.Events.Any(x=>x.Chip==NativeChip.Scc&&x.Port==4),label+": stopped morph continued rewriting the static wave.");
            }
        }
        Console.WriteLine("PASS WM zero/carry/rest and static wave/gate/envelope/release: PN2/3/4/5/6 x WP0/1/2 x ?/@ aliases");
    }
}
