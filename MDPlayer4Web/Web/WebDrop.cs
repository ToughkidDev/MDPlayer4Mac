// Entry points for wwwroot/main.js's drag-and-drop handling (see MainWindow.Web.cs).
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace MDPlayer.UI
{
    // MDPlayer has its own Point/Size compatibility types; these files mean Avalonia's.
    using Point = Avalonia.Point;
    using Size = Avalonia.Size;

    internal static partial class WebDrop
    {
        // Shows the browser's file chooser (multiple files). Resolves to a JSON object
        // { folder, names } once the chosen files have been copied in with AddFile.
        [JSImport("pickFiles", "mdplayer")]
        internal static partial Task<string> PickFiles();

        [JSExport]
        internal static string Begin() => WebFileSystem.NewBatchFolder();

        [JSExport]
        internal static void AddFile(string folder, string relativePath, byte[] data)
            => WebFileSystem.Write(folder, relativePath, data);

        [JSExport]
        internal static void Over(double x, double y) => WebApp.MainWindow?.WebDragOver(new Point(x, y));

        [JSExport]
        internal static void Leave() => WebApp.MainWindow?.WebDragLeave();

        // topLevelNames: JSON array of the dropped files/folders, relative to folder.
        [JSExport]
        internal static void Complete(string folder, string topLevelNames, double x, double y)
        {
            string[] names = JsonDocument.Parse(topLevelNames).RootElement.EnumerateArray()
                .Select(element => element.GetString() ?? string.Empty)
                .Where(name => name.Length > 0)
                .ToArray();
            IStorageItem[] items = names.Select(name => VfsStorage.Item(Path.Combine(folder, name))).ToArray();
            Dispatcher.UIThread.Post(async () =>
            {
                if (WebApp.MainWindow is MainWindow window) await window.WebDropAsync(items, new Point(x, y));
            });
        }
    }
}
