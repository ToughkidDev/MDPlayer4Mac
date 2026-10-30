// Browser-only additions to the shared MainWindow (macos/MDPlayerUI/MainWindow.axaml.cs).
//
// Avalonia.Browser's own drag-and-drop yields browser file handles without paths, which
// would lose the companion files a song needs. wwwroot/main.js therefore takes the drop
// itself, copies everything (folders included) into the in-memory file system and
// reports it here; these methods mirror MainWindow's own drag handlers on top of that.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MDPlayer.UI.Visualizer;

namespace MDPlayer.UI
{
    // MDPlayer has its own Point/Size compatibility types; these files mean Avalonia's.
    using Point = Avalonia.Point;
    using Size = Avalonia.Size;

    public partial class MainWindow
    {
        // null while nothing is being dragged over the page.
        private bool? webDragOverDashboard;

        internal bool WebIsOverDashboard(Point pagePoint)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || PlayerDashboard.TranslatePoint(new Point(0, 0), topLevel) is not Point origin) return false;
            return new Rect(origin, PlayerDashboard.Bounds.Size).Contains(pagePoint);
        }

        // OnDashboardDragEnter, or OnPlaylistDragEnter / OnFileDragEnter elsewhere.
        internal void WebDragOver(Point pagePoint)
        {
            bool dashboard = WebIsOverDashboard(pagePoint);
            if (webDragOverDashboard == dashboard) return;
            WebDragLeave();
            webDragOverDashboard = dashboard;
            if (dashboard) StatusLabel.Text = "음악 파일 또는 폴더를 놓으면 재생 목록을 교체합니다";
            else PlaylistHint.Text = "음악 파일 또는 폴더를 놓으면 재생 목록에 추가합니다";
        }

        // OnDashboardDragLeave / OnFileDragLeave
        internal void WebDragLeave()
        {
            if (webDragOverDashboard == true && !isStarting)
            {
                StatusLabel.Text = isPaused
                    ? "일시 정지"
                    : queue != null && !playbackEnded ? "재생 중" : "대기 중";
            }
            else if (webDragOverDashboard == false)
            {
                RefreshPlaylistPanel();
            }
            webDragOverDashboard = null;
        }

        // HandleDroppedFilesAsync, starting from the already expanded dropped items.
        internal async Task WebDropAsync(IStorageItem[] droppedItems, Point pagePoint)
        {
            bool replacePlaylist = WebIsOverDashboard(pagePoint);
            webDragOverDashboard = null;
            if (droppedItems.Length == 0)
            {
                PlaylistHint.Text = "지원하는 음악 파일 또는 폴더를 드롭하세요";
                return;
            }

            System.Collections.Generic.List<IStorageFile> files = await ExpandDroppedStorageItemsAsync(droppedItems);
            if (files.Count == 0)
            {
                PlaylistHint.Text = "폴더 안에 지원하는 음악 파일이 없습니다";
                return;
            }

            System.Collections.Generic.List<PlaylistEntry> droppedEntries = await ReadPlaylistEntriesAsync(files);
            int added = droppedEntries.Count;
            if (added == 0) return;

            if (replacePlaylist)
            {
                StopPlayback();
                playlist.Clear();
                playlist.AddRange(droppedEntries);
                playlistIndex = -1;
                await SelectPlaylistEntryAsync(0);
                if (autoPlayEnabled) await StartPlaybackAsync();
                return;
            }

            bool wasEmpty = playlist.Count == 0;
            playlist.AddRange(droppedEntries);
            if (wasEmpty)
            {
                StopPlayback();
                HideMixer();
                HideVisualizers();
                await SelectPlaylistEntryAsync(0);
                if (autoPlayEnabled) await StartPlaybackAsync();
            }
            else
            {
                StatusLabel.Text = $"재생 목록에 {added}곡을 추가했습니다";
                RefreshPlaylistPanel();
                UpdateTransportButtons();
            }
        }

        // The shared Open button asks Avalonia's storage provider for files, which in a
        // browser are path-less handles. Swap in a button with the same sprite, tooltip and
        // shortcut that goes through the page's own chooser instead (see OnOpenClick).
        internal void WebInitialize()
        {
            TransportSpriteButton webOpenButton = MakeTransportButton("OpenFolder", "파일 열기", () => _ = WebOpenAsync());
            int index = UtilityButtonsHost.Children.IndexOf(openButton.Screen);
            UtilityButtonsHost.Children[index] = webOpenButton.Screen;
            openButton = webOpenButton;
            dashboardKeyBindings[Avalonia.Input.Key.A] = webOpenButton;
            UpdateTransportButtons();
            // In a page, dropping files or folders onto the player is the main way in.
            FileLabel.Text = "폴더나 파일들을 드래그해서 플레이어에 드롭해 주세요.";
        }

        // OnOpenClick
        private async Task WebOpenAsync()
        {
            string picked;
            try
            {
                picked = await WebDrop.PickFiles();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"오류: {ex.Message}";
                return;
            }

            using var document = System.Text.Json.JsonDocument.Parse(picked);
            string folder = document.RootElement.GetProperty("folder").GetString() ?? string.Empty;
            IStorageFile[] files = document.RootElement.GetProperty("names").EnumerateArray()
                .Select(name => VfsStorage.Item(System.IO.Path.Combine(folder, name.GetString() ?? string.Empty)))
                .OfType<IStorageFile>()
                .ToArray();
            if (files.Length < 1) return;

            StopPlayback();
            HideVisualizers();
            HideMixer();

            playlist.Clear();
            await AppendFilesAsync(files.Where(IsSupportedInputFile));
            if (playlist.Count == 0)
            {
                StatusLabel.Text = "지원하는 음악 파일을 선택하세요";
                RefreshPlaylistPanel();
                UpdateTransportButtons();
                return;
            }

            await SelectPlaylistEntryAsync(0);
            if (autoPlayEnabled) await StartPlaybackAsync();
        }

        internal void WebSaveSettings()
        {
            try { loadedSession?.Setting.Save(); } catch { }
        }
    }
}
