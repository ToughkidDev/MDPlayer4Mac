// File access for the browser build.
//
// The macOS player passes each song's real path to MusicEngine so drivers can open the
// files that belong next to it (MDX -> PDX, PMD -> PPC/P86, MUC/MML -> voice and PCM data,
// .m3u entries ...). A browser only hands over the files the user picked or dropped, so
// they are copied into the runtime's in-memory file system, one folder per pick/drop,
// keeping the dropped folder structure. The player then sees ordinary file:// items whose
// companions sit beside them exactly as on disk.
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Platform.Storage;

namespace MDPlayer.UI
{
    internal static class WebFileSystem
    {
        public const string MusicRoot = "/music";
        private static int batch;

        public static string NewBatchFolder()
        {
            string folder = $"{MusicRoot}/{++batch}";
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string Write(string folder, string relativePath, byte[] data)
        {
            string path = Path.GetFullPath(Path.Combine(folder, relativePath.Replace('\\', '/').TrimStart('/')));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
            DriverLibrary.CaptureIfDriver(path, data);
            return path;
        }
    }

    // Avalonia's file-system-backed storage items (BclStorageFile/BclStorageFolder) are
    // internal and its storage interfaces cannot be implemented outside Avalonia, so the
    // items for files in the in-memory file system are created through reflection.
    internal static class VfsStorage
    {
        private const string FileType = "Avalonia.Platform.Storage.FileIO.BclStorageFile";
        private const string FolderType = "Avalonia.Platform.Storage.FileIO.BclStorageFolder";

        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicConstructors, FileType, "Avalonia.Base")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicConstructors, FolderType, "Avalonia.Base")]
        public static IStorageItem Item(string path)
            => Directory.Exists(path)
                ? (IStorageItem)Create(FolderType, new DirectoryInfo(path))
                : (IStorageItem)Create(FileType, new FileInfo(path));

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Constructors kept by DynamicDependency on Item.")]
        private static object Create(string typeName, FileSystemInfo info)
        {
            Type type = typeof(IStorageItem).Assembly.GetType(typeName, throwOnError: true)!;
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { info }, null)!;
        }
    }
}
