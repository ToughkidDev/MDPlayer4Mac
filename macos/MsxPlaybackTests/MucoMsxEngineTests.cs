using System.Reflection;
using System.Text;
using MDPlayer.Driver.MUCOM;
using musicDriverInterface;

// Playback rules where the mucomDotNET driver is made to follow MucoMSX (MUBPLAY), the
// reference player: the detune is added to the 16-bit block/F-number value without
// moving the block, and an SSG part still takes its last software-LFO step on the tick
// it ends.
internal static class MucoMsxEngineTests
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run(Action<bool, string> check, string fixtures)
    {
        // FM detune across the octave edge (MUCOM88 v1.7 FMGFQ): b (F-number 1167) + 100
        // stays in its block as 1267, c (618) - 100 stays as 518.
        foreach ((string note, int detune, int expected) in new[] { ("b", 100, 1167 + 100), ("c", -100, 618 - 100) })
        {
            var ticks = Play(check, fixtures, $"#driver mucomDotNET\nA t200 o4 l4 D{detune} {note}\n", 8);
            int? Last(int address) => ticks.SelectMany(t => t).Where(w => w.Port == 0 && w.Address == address).Select(w => (int?)w.Value).LastOrDefault();
            var plain = Play(check, fixtures, $"#driver mucomDotNET\nA t200 o4 l4 {note}\n", 8);
            int? plainHigh = plain.SelectMany(t => t).Where(w => w.Port == 0 && w.Address == 0xa4).Select(w => (int?)w.Value).LastOrDefault();
            int? high = Last(0xa4), low = Last(0xa0);
            check(high != null && low != null && plainHigh != null, "FM detune test wrote no F-number.");
            check(((high & 7) << 8 | low) == expected && (high >> 3) == (plainHigh >> 3),
                $"FM detune {detune:+0;-0} on {note} moved the block or F-number ({high:X2} {low:X2}).");
        }

        // SSG software LFO (every tick) on a note that ends the part: the tick the part
        // ends still writes one more LFO step, later ticks write none.
        var ssg = Play(check, fixtures, "#driver mucomDotNET\nD t200 o4 l16 M0,1,3,20 c\n", 40);
        int end = -1;
        for (int t = 0; t < ssg.Count; t++)
            if (ssg[t].Any(w => w.Port == 0 && w.Address == 8 && (w.Value & 15) == 0)) end = t;
        check(end > 0, "SSG LFO test did not end.");
        bool Freq(int t) => ssg[t].Any(w => w.Port == 0 && w.Address is 0 or 1);
        check(Freq(end - 1) && Freq(end), "SSG part skipped its last LFO step on the end tick.");
        check(Enumerable.Range(end + 1, ssg.Count - end - 1).All(t => !Freq(t)), "SSG LFO continued after the part ended.");
        Console.WriteLine("PASS MucoMSX engine: FM detune at the octave edge, last SSG LFO step");
    }

    private sealed record Write(int Port, int Address, int Value);

    private static List<List<Write>> Play(Action<bool, string> check, string fixtures, string score, int count)
    {
        byte[] mub = MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(score), Path.Combine(fixtures, "engine.muc"), out string error);
        check(mub != null, "Engine test compile: " + error);
        var ticks = new List<List<Write>>();
        List<Write> current = new();
        var driver = new mucomDotNET.Driver.Driver();
        var actions = new List<ChipAction>();
        for (int c = 0; c < 5; c++)
        {
            int chip = c;
            actions.Add(new Sink(d => { if (chip == 0 && d.port >= 0 && d.address >= 0 && d.data >= 0) current.Add(new Write(d.port, d.address, d.data)); }));
        }
        driver.Init(actions, mub.Select(b => new MmlDatum(b)).ToArray(), _ => null, new object[] { false, true, false, "engine.mub" });
        driver.StartRendering(44100, new[] { Tuple.Create("", 7987200), Tuple.Create("", 7987200), Tuple.Create("", 8000000), Tuple.Create("", 8000000), Tuple.Create("", 3579545) });
        driver.MusicSTART(0);
        object music = driver.GetType().GetField("music2", F)!.GetValue(driver)!;
        MethodInfo tick = music.GetType().GetMethod("PL_SND", F)!;
        current.Clear();
        for (int t = 0; t < count; t++)
        {
            current = new List<Write>();
            tick.Invoke(music, null);
            ticks.Add(current);
        }
        return ticks;
    }

    private sealed class Sink : ChipAction
    {
        private readonly Action<ChipDatum> write;
        public Sink(Action<ChipDatum> write) => this.write = write;
        public override string GetChipName() => "test";
        public override void WriteRegister(ChipDatum value) => write(value);
        public override void WritePCMData(byte[] data, int startAddress, int endAddress) { }
        public override void WaitSend(long elapsed, int size) { }
    }
}
