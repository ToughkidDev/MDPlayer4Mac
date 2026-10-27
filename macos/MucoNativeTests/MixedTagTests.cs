using System.Text;
using MDPlayer.MucoNative;

// OPNA driver tags (#CarrierCorrection, #OPNA1RhythmMute) survive when MSX parts
// turn the song into a mixed packed container.
internal static class MixedTagTests
{
    internal static void Run(Action<bool,string> check)
    {
        const string wave="?WD0={0,25,49,71,90,106,117,125,127,125,117,106,90,71,49,25,0,-25,-49,-71,-90,-106,-117,-125,-128,-125,-117,-106,-90,-71,-49,-25}\n";
        // Algorithm 4: carriers OP2 and OP4, written 24 apart.
        const string voice="  @1\n   0,4\n   31,0,0,15,0,30,0,1,0\n   31,0,0,15,0,0,0,1,0\n   31,0,0,15,0,30,0,1,0\n   31,0,0,15,0,24,0,1,0\n";
        List<NativeChipWrite> Render(string score)
        {
            var writes=new List<NativeChipWrite>();
            using var s=new NativeMucomSession(NativeMucCompiler.Compile(Encoding.ASCII.GetBytes(score),_=>null),44_100,writes.Add);
            while(s.Status>0&&s.Samples<1_000_000)s.AdvanceSample();
            check(s.Status==0&&s.Error=="","Mixed tag playback failed.");return writes;
        }
        int LastTl(List<NativeChipWrite> w,int address)=>w.Last(x=>x.Chip==NativeChip.Opna1&&x.Port==0&&x.Address==address).Value;
        foreach(string msx in new[]{"SCCA ?XU7 ?XT1 @0 v8 o4 c%8 !\n","MSXA ?XU7 ?XT1 ?IR3 v12 o4 c%8 !\n"})
        {
            string fm="A t198 C112 @1 v12 o4 c4 v8 c4\n";
            var on=Render("#CarrierCorrection Yes\n"+wave+voice+fm+msx);
            check(LastTl(on,0x4c)-LastTl(on,0x48)==24,"#CarrierCorrection was ignored beside "+msx[..4]+".");
            var off=Render(wave+voice+fm+msx);
            check(LastTl(off,0x4c)==LastTl(off,0x48),"Carriers were corrected without #CarrierCorrection beside "+msx[..4]+".");
        }
        string drums="A t198 C112 o4 c4\nG C112 v50,30,30,30,30,30,30 @1c4 @2c4\nSCCA ?XU7 ?XT1 @0 v8 o4 c%8 !\n";
        bool Snare(List<NativeChipWrite> w)=>w.Any(x=>x.Chip==NativeChip.Opna1&&x.Port==0&&x.Address==0x10&&(x.Value&0x82)==0x02);
        check(Snare(Render(wave+drums)),"The rhythm snare did not play beside SCC.");
        check(!Snare(Render("#OPNA1RhythmMute S\n"+wave+drums)),"#OPNA1RhythmMute was ignored beside SCC.");
        Console.WriteLine("PASS mixed OPNA tags: #CarrierCorrection and #OPNA1RhythmMute beside SCC/MSX-MUSIC");
    }
}
