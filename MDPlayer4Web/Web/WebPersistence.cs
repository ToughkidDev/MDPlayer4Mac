// Keeps the player's settings and the user's driver files across visits.
//
// On macOS these live on disk (Setting.xml in the application-data folder and the
// Drivers folder from DriverFiles.UserDriversFolder). Here they live in the runtime's
// in-memory file system, so audio.js restores them from IndexedDB before Main runs and
// this class mirrors every change back.
using System.Runtime.InteropServices.JavaScript;
using Avalonia.Threading;

namespace MDPlayer.UI
{
    internal static partial class WebPersistence
    {
        private static readonly Dictionary<string, (long Length, DateTime Modified)> known = new(StringComparer.Ordinal);
        private static DispatcherTimer? timer;

        [JSImport("persistFile", "mdplayer")]
        private static partial void PersistFile(string path, byte[] data);

        [JSImport("forgetFile", "mdplayer")]
        private static partial void ForgetFile(string path);

        // Called by main.js for every saved file before the application starts.
        [JSExport]
        internal static void Restore(string path, byte[] data)
        {
            if (!IsPersistent(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
            known[path] = Stamp(path);
        }

        [JSExport]
        internal static string[] Roots() => RootFolders().ToArray();

        // Page is being closed: save the settings as MainWindow does when its window closes.
        [JSExport]
        internal static void Flush()
        {
            WebApp.MainWindow?.WebSaveSettings();
            Sync();
        }

        public static void Start()
        {
            Dispatcher.UIThread.Post(() =>
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) => Sync();
                timer.Start();
            });
        }

        private static IEnumerable<string> RootFolders()
        {
            // Relative when the platform reports no application-data folder, as in a browser.
            string? settings = Common.GetApplicationDataFolder(true);
            if (!string.IsNullOrEmpty(settings)) yield return Path.GetFullPath(settings);
            string? drivers = DriverFiles.UserDriversFolder;
            if (!string.IsNullOrEmpty(drivers)) yield return drivers;
        }

        private static bool IsPersistent(string path)
            => RootFolders().Any(root => path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal))
               && !path.Contains("/operation/", StringComparison.Ordinal);

        private static (long, DateTime) Stamp(string path)
        {
            var info = new FileInfo(path);
            return (info.Length, info.LastWriteTimeUtc);
        }

        public static void Sync()
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (string root in RootFolders())
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!IsPersistent(path)) continue;
                    present.Add(path);
                    var stamp = Stamp(path);
                    if (known.TryGetValue(path, out var old) && old == stamp) continue;
                    try
                    {
                        PersistFile(path, File.ReadAllBytes(path));
                        known[path] = stamp;
                    }
                    catch (IOException) { }
                }
            }
            foreach (string gone in known.Keys.Where(path => !present.Contains(path)).ToArray())
            {
                known.Remove(gone);
                ForgetFile(gone);
            }
        }
    }

    // Driver programs and ROM images the user opens or drops are also kept in the user's
    // Drivers folder (the page's equivalent of copying them there in Finder), so they are
    // found for every later song.
    internal static class DriverLibrary
    {
        private static readonly HashSet<string> driverNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "MGSDRV.COM", "KINROU5.DRV", "KINROU4.COM", "NDP.BIN", "FMP.COM", "FMC.EXE",
            "ZMUSIC.X", "ZMC.X", "ZMSC3.X", "yrw801.rom",
        };

        public static void CaptureIfDriver(string path, byte[] data)
        {
            string? folder = DriverFiles.UserDriversFolder;
            if (string.IsNullOrEmpty(folder) || !driverNames.Contains(Path.GetFileName(path))) return;
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, Path.GetFileName(path)), data);
        }
    }
}
