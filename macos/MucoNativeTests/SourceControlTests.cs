using System.Text.Json;
using MDPlayer.MucoNative;

internal static class SourceControlTests
{
    internal static void Run(Action<bool,string> check)
    {
        (PackedMusicPlayer P,List<NativeChipWrite> E) Make(string s)
        {
            var o=MsxMusicalCompiler.Parse(s,_=>null);var e=new List<NativeChipWrite>();
            return(new(new(o.Object),e.Add,_=>{}),e);
        }
        int Last(List<NativeChipWrite> e,int a)=>e.Last(x=>x.Chip==NativeChip.Opll&&x.Address==a).Value;
        string table="?KT0={344,364,386,409,433,459,486,516,546,578,612,648}\n";
        var pitch=Make(table+"MSXA ?XU1 ?XT0 ?XT1 ?KT0 ?QP128,1 @1 o4 c%8 !\n");pitch.P.Tick();
        check(Last(pitch.E,16)==88,"OPLL compact KT did not apply the selected F-number.");
        var octave=Make(table+"MSXA ?XU1 ?XT0 ?XT1 ?KT0 @1 o4 g%2 !\n");octave.P.Tick();
        check(Last(octave.E,16)==2,"OPLL KT >=512 did not halve the F-number.");
        var zero=Make("MSXA ?XU1 ?QE1 @1 o4 c%2 !\n");for(int i=0;i<3;i++)zero.P.Tick();
        check(zero.P.Ended&&Last(zero.E,16)==0&&(Last(zero.E,32)&31)==16,"QE1 did not retire the route with the source zero-frequency key state.");
        var legacy=Make("MSXA ?XU1 @1 o4 c%2 !\n");for(int i=0;i<3;i++)legacy.P.Tick();
        check((Last(legacy.E,32)&16)==0,"Default OPLL end behavior changed.");
        string kit="?IT0={224,4,190,3,5,1}\n";
        var rest=Make(kit+"MSXG ?XU1 ?XT64 ?IT0,1 v14 ?ID-,2 c%2 !\n");rest.P.Tick();
        check(Last(rest.E,22)==224&&Last(rest.E,55)==48&&Last(rest.E,14)==0,"Rhythm rest delayed its pending kit/volume or failed to disable rhythm.");
        var held=Make(kit+"MSXG ?XU1 ?QE2 ?XT64 ?IT0,1 v14 c%2 !\n");for(int i=0;i<3;i++)held.P.Tick();
        check(held.P.Ended&&(Last(held.E,14)&32)!=0,"QE2 did not retain the physical rhythm state.");
        var noise=Make("MSXD ?XU1 ?NP-,3 ?XT1 ?NP5 v12 c%4 !\nMSXF ?XU1 ?XT1 ?NP9 v12 c%4 !\n");noise.P.Tick();
        check(noise.E.Last(x=>x.Chip==NativeChip.Psg&&x.Address==6).Value==9,"Noise policy 3 did not use the last music-channel writer.");
        byte[] Work(PackedMusicPlayer p){using var j=JsonDocument.Parse(JsonSerializer.Serialize(p.Snapshot()));return j.RootElement[8].GetProperty("w").GetBytesFromBase64();}
        var fresh=Make("MSXA ?XU1 ?QP255,1 @1 o4 c%3 ?QP64 d%16 !\n");
        for(int i=0;i<4;i++)fresh.P.Tick();
        check(Work(fresh.P)[41]==4,"Fresh QP gate mode accumulated the preceding note's gate counter.");
        foreach(string bad in new[]{table+"MSXG ?XU1 ?KT0 c !\n",table.Replace("344","0")+"MSXA ?XU1 ?KT0 c !\n","MSXA ?XU1 ?QE3 c !\n",kit+"MSXG ?XU1 ?IT0,1,1 c !\n","MSXA ?XU1 ?QP1,2 c !\n"})
        {
            bool rejected=false;try{var x=Make(bad);x.P.Tick();}catch(FormatException){rejected=true;}
            check(rejected,"Invalid OPLL scope/operand accepted: "+bad);
        }
        Console.WriteLine("PASS source controls: OPLL KT, fresh QP, rhythm rest/kit/bias, QE policies and shared PSG noise");
    }
}
