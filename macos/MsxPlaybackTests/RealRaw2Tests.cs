using MDPlayer;
using MDPlayer.Driver.MUCOM;
using System.Text.Json;

// Optional local corpus: commercial VGMs and converted files are never bundled.
internal static class RealRaw2Tests
{
    internal static void Run(string manifest,Action<bool,string> check)
    {
        foreach(var row in JsonDocument.Parse(File.ReadAllText(manifest)).RootElement.EnumerateArray())
        {
            bool loop=row.GetProperty("loop").GetBoolean();
            var sessions=new List<MusicEngineSession>();
            foreach(string kind in new[]{"original","muc","mub"})
            {
                string path=row.GetProperty(kind).GetString()!;
                var session=MusicEngine.Load(File.ReadAllBytes(path),path);
                check(session!=null,"Real input failed to load: "+path);
                short[] samples=new short[4096];long energy=0;int peak=0;
                for(int i=0;i<13000 && !session!.Driver.Stopped && (!loop || session.Driver.vgmCurLoop<3);i++)
                {
                    int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
                    foreach(short value in samples.Take(n)){energy+=Math.Abs((int)value);peak=Math.Max(peak,Math.Abs((int)value));}
                }
                check(energy>0,"Real input rendered silence: "+path);
                check(loop?session!.Driver.vgmCurLoop>=3:session!.Driver.Stopped,"Real song/three laps incomplete: "+path);
                if(session!.Driver is MucomDotNET driver)check(string.IsNullOrEmpty(driver.PlaybackError),"Real playback error: "+driver.PlaybackError);
                sessions.Add(session);
                Console.WriteLine($"PASS REAL {kind} {Path.GetFileName(path)} {session.DescribeActiveChips()} peak={peak} loops={session.Driver.vgmCurLoop}");
            }
            if(row.GetProperty("chips").EnumerateArray().Any(c=>c.GetString()=="scc"))
            {
                var original=sessions[0].ChipRegister.GetK051649Register(0).channel_list;
                foreach(var session in sessions.Skip(1))
                {
                    check(session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.K051649),"Missing real SCC chip");
                    var converted=session.ChipRegister.GetK051649Register(0).channel_list;
                    for(int c=0;c<5;c++)
                    {
                        check(original[c].waveram.SequenceEqual(converted[c].waveram),"Real SCC waveform differs: channel "+c);
                        check(original[c].frequency==converted[c].frequency,"Real SCC period differs: channel "+c);
                    }
                }
            }
        }
    }
}
