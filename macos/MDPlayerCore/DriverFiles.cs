using System.IO;

namespace MDPlayer
{
    // Locates the original driver programs and ROM images some formats execute or read
    // (MGSDRV.COM, KINROU5.DRV, NDP.BIN, FMP.COM, ZMUSIC.X, yrw801.rom, ...).
    //
    // The Windows build looks for these next to MDPlayerx64.exe. On macOS that directory is
    // MDPlayer4Mac.app/Contents/MacOS: users cannot add files there without breaking the
    // code signature, and an app update replaces it. Look in a per-user folder first, then
    // in the Drivers folder shipped inside the bundle (MDPlayerUI/Assets/Drivers).
    public static class DriverFiles
    {
        public const string DriversFolderName = "Drivers";

        // ~/Library/Application Support/MDPlayer4Mac/Drivers on macOS. Shown in the settings
        // window so users know where to drop their own driver files.
        public static string UserDriversFolder
        {
            get
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (OperatingSystem.IsMacOS() && !string.IsNullOrEmpty(home))
                    return Path.Combine(home, "Library", "Application Support", "MDPlayer4Mac", DriversFolderName);
                string data = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return string.IsNullOrEmpty(data) ? null : Path.Combine(data, "MDPlayer4Mac", DriversFolderName);
            }
        }

        public static string BundledDriversFolder => Path.Combine(Common.GetApplicationFolder(), DriversFolderName);

        // Returns the full path of fileName, or null when it is not found anywhere.
        // explicitPath (a path chosen in Settings) wins when it exists.
        public static string Find(string fileName, string explicitPath = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;
            if (string.IsNullOrEmpty(fileName)) return null;

            foreach (string folder in SearchFolders())
            {
                string found = FindInFolder(folder, fileName);
                if (found != null) return found;
            }
            return null;
        }

        // Folder that contains fileName, for code that takes a directory rather than a file
        // (e.g. ymf278b's yrw801.rom loader). Falls back to the application folder.
        public static string FindFolder(string fileName)
        {
            string found = Find(fileName);
            return found != null ? Path.GetDirectoryName(found) : Common.GetApplicationFolder();
        }

        private static System.Collections.Generic.IEnumerable<string> SearchFolders()
        {
            string user = UserDriversFolder;
            if (!string.IsNullOrEmpty(user)) yield return user;
            yield return BundledDriversFolder;
            // Legacy Windows layout: files placed directly beside the executable.
            yield return Common.GetApplicationFolder();
        }

        // DOS-era file names are conventionally upper case, but users often store them in
        // lower case; macOS volumes are usually case-insensitive, Linux ones are not.
        private static string FindInFolder(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
            string direct = Path.Combine(folder, fileName);
            if (File.Exists(direct)) return direct;
            try
            {
                foreach (string candidate in Directory.EnumerateFiles(folder))
                {
                    if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }
    }
}
