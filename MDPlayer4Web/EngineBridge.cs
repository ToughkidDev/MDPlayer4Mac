using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;

namespace MDPlayer.Web
{
    // JavaScript-facing wrapper around MusicEngine. It runs inside the engine Web Worker
    // (wwwroot/engine-worker.js); the worker writes the user's files into the runtime's
    // in-memory file system so drivers find companion files (PCM, PDX, voice data, rhythm
    // WAVs) beside the song exactly as on macOS.
    public static partial class EngineBridge
    {
        private static MusicEngineSession? session;
        private static long fileLoopCounter;
        private static bool loopEnabled;
        private static short[] pcm = Array.Empty<short>();
        private static byte[] pcmBytes = Array.Empty<byte>();

        public static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // Some emulator cores print per-instruction traces to stdout; in the browser each
            // line becomes a console.log call that stalls rendering, so stdout is discarded.
            Console.SetOut(TextWriter.Null);
        }

        // Writes a file into the in-memory file system, creating its folder.
        [JSExport]
        internal static void WriteFile(string path, byte[] data)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, data);
        }

        // Where original driver programs and ROM images (MGSDRV.COM, NDP.BIN, FMP.COM,
        // yrw801.rom, ...) are looked up; the page restores saved ones here at start-up.
        [JSExport]
        internal static string DriversFolder() => DriverFiles.UserDriversFolder ?? "/drivers";

        // The engine's application folder (AppContext.BaseDirectory). Drivers fall back to
        // it for shared data such as the YM2608 rhythm samples, which the page installs here.
        [JSExport]
        internal static string AppFolder() => Common.GetApplicationFolder();

        // Loads a song already written with WriteFile. Returns a JSON description; "error"
        // is set when the file cannot be played.
        [JSExport]
        internal static string Load(string path, bool loop)
        {
            Unload();
            try
            {
                byte[] data = File.ReadAllBytes(path);
                MusicEngineSession? loaded = MusicEngine.Load(data, path);
                if (loaded == null)
                {
                    string reason = MusicEngine.LastLoadError;
                    return Json(("error", string.IsNullOrWhiteSpace(reason) ? "지원하지 않는 포맷/칩이거나 필요한 파일이 없습니다." : reason));
                }

                session = loaded;
                fileLoopCounter = loaded.Driver.LoopCounter;
                loopEnabled = loop;
                loaded.Driver.LoopCounter = loop ? fileLoopCounter : 0;
                GD3 gd3 = loaded.Driver.GD3 ?? new GD3();
                return Json(
                    ("format", loaded.Format.ToString()),
                    ("chips", loaded.DescribeActiveChips()),
                    ("sampleRate", loaded.SampleRate),
                    ("title", Pick(gd3.TrackNameJ, gd3.TrackName)),
                    ("game", Pick(gd3.GameNameJ, gd3.GameName)),
                    ("system", Pick(gd3.SystemNameJ, gd3.SystemName)),
                    ("composer", Pick(gd3.ComposerJ, gd3.Composer)),
                    ("total", Seconds(loaded.Driver.TotalCounter)),
                    ("loopLength", Seconds(fileLoopCounter)));
            }
            catch (Exception ex)
            {
                Unload();
                return Json(("error", ex.Message));
            }
        }

        // Renders up to `frames` stereo frames as little-endian 16-bit PCM. Returns an empty
        // array when nothing is loaded or the song has ended.
        [JSExport]
        internal static byte[] Render(int frames)
        {
            MusicEngineSession? s = session;
            if (s == null || s.Driver.Stopped || frames <= 0) return Array.Empty<byte>();
            int count = frames * 2;
            if (pcm.Length != count) { pcm = new short[count]; pcmBytes = new byte[count * 2]; }
            int written;
            try
            {
                written = s.RenderSamplesWithMasterVolume(pcm, 0, count);
            }
            catch (Exception)
            {
                s.Driver.Stopped = true;
                return Array.Empty<byte>();
            }
            if (written <= 0) return Array.Empty<byte>();
            Buffer.BlockCopy(pcm, 0, pcmBytes, 0, written * 2);
            return written == count ? pcmBytes : pcmBytes.AsSpan(0, written * 2).ToArray();
        }

        // Playback position as JSON: seconds played, total length (0 when unknown), loop
        // count reached, and whether the driver has finished.
        [JSExport]
        internal static string Status()
        {
            MusicEngineSession? s = session;
            if (s == null) return "{\"loaded\":false}";
            return Json(
                ("loaded", true),
                ("position", Seconds(Math.Max(0, s.Driver.Counter))),
                ("total", Seconds(s.Driver.TotalCounter)),
                ("loops", s.Driver.vgmCurLoop),
                ("stopped", s.Driver.Stopped));
        }

        // Same rule as the macOS player: with repeat on the driver keeps the file's loop
        // count, otherwise it plays once and stops.
        [JSExport]
        internal static void SetLoop(bool loop)
        {
            loopEnabled = loop;
            if (session != null) session.Driver.LoopCounter = loop ? fileLoopCounter : 0;
        }

        [JSExport]
        internal static void Unload()
        {
            if (session?.Driver is IDisposable disposable) disposable.Dispose();
            session = null;
        }

        private static double Seconds(long counter) => counter <= 0 ? 0 : counter / (double)Common.VGMProcSampleRate;
        private static string Pick(string? preferred, string? fallback) => !string.IsNullOrWhiteSpace(preferred) ? preferred! : fallback ?? "";
        // Small flat-object writer. Reflection-based JsonSerializer is not available in the
        // trimmed WebAssembly build, and these messages only carry strings, numbers and flags.
        private static string Json(params (string Key, object? Value)[] properties)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var (key, value) in properties)
                {
                    switch (value)
                    {
                        case null: writer.WriteNull(key); break;
                        case bool b: writer.WriteBoolean(key, b); break;
                        case double d: writer.WriteNumber(key, d); break;
                        case uint u: writer.WriteNumber(key, u); break;
                        case int i: writer.WriteNumber(key, i); break;
                        case long l: writer.WriteNumber(key, l); break;
                        default: writer.WriteString(key, value.ToString()); break;
                    }
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
