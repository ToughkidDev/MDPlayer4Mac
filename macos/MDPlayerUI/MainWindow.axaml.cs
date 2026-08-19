// Wires the Avalonia UI to the same VgmEngine + CoreAudioQueue plumbing LivePlayer uses
// from the command line (see macos/LivePlayer/Program.cs) - this window is just a GUI
// front-end over identical playback logic, nothing about the engine/audio layer changes.
//
// Threading model: VgmEngine.Load (file parsing) and CoreAudioQueue construction/Start
// (native P/Invoke calls) are pushed onto background threads via Task.Run so the UI
// thread never blocks. CoreAudioQueue's own buffer-refill callbacks run on a thread
// CoreAudio manages internally (we pass IntPtr.Zero as the run loop in
// CoreAudioQueue's AudioQueueNewOutput call), not the Avalonia UI thread - only the
// small amount of UI-updating code here needs Dispatcher.UIThread.InvokeAsync to hop
// back onto the UI thread safely.
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MDPlayer;
using MDPlayer.CoreAudioOutput;

namespace MDPlayer.UI
{
    public partial class MainWindow : Window
    {
        private const int FramesPerBuffer = 2048;
        private const int BufferCount = 4;

        private byte[]? loadedVgmBytes;
        private string? loadedFileName;
        private CoreAudioQueue? queue;
        private volatile bool stopRequested;

        public MainWindow()
        {
            InitializeComponent();
            Closing += (_, _) => StopPlayback();
        }

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "음악 파일 열기",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    // .vgz is just a gzip-compressed .vgm (the format most real-world VGM
                    // downloads come in, e.g. from vgmrips.net) - VgmEngine.Load decompresses
                    // it transparently, so both extensions are equally valid input here.
                    // The rest are the other formats MusicEngine.cs dispatches on (see its
                    // header comment for the full list and per-format verification status).
                    new FilePickerFileType("Music files")
                    {
                        Patterns = new[]
                        {
                            "*.vgm", "*.vgz", "*.xgm", "*.xgz", "*.sid", "*.mnd", "*.zms", "*.zmd",
                            "*.mdx", "*.mdr", "*.nsf", "*.gbs", "*.hes", "*.s98", "*.ay", "*.zgm",
                        },
                    },
                    FilePickerFileTypes.All,
                },
            });

            if (files.Count < 1) return;

            StopPlayback();

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            loadedVgmBytes = ms.ToArray();
            loadedFileName = file.Name;

            FileLabel.Text = file.Name;
            StatusLabel.Text = $"로드됨 ({loadedVgmBytes.Length} bytes) - 재생 준비";
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private async void OnPlayClick(object? sender, RoutedEventArgs e)
        {
            byte[]? vgmBuf = loadedVgmBytes;
            string? fileName = loadedFileName;
            if (vgmBuf == null) return;

            OpenButton.IsEnabled = false;
            PlayButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusLabel.Text = "로딩 중...";

            stopRequested = false;

            try
            {
                MusicEngineSession? loadedSession = await Task.Run(() => MusicEngine.Load(vgmBuf, fileName));
                if (loadedSession == null || stopRequested)
                {
                    if (!stopRequested)
                        StatusLabel.Text = "오류: 이 파일은 재생할 수 없습니다 (지원하지 않는 포맷/칩, MusicEngine.cs 참고)";
                    OpenButton.IsEnabled = true;
                    PlayButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    return;
                }

                await Task.Run(() =>
                {
                    CoreAudioQueue localQueue = new(
                        loadedSession.SampleRate, FramesPerBuffer, BufferCount,
                        (buf, count) =>
                        {
                            if (stopRequested || loadedSession.Driver.Stopped) return 0;
                            // loadedSession.RenderSamples, not Mds.Update() directly - see
                            // EngineSmokeTest/Program.cs's identical comment (SID/NSF/MDX
                            // bypass MDSound.MDSound.Chip.Update() entirely and pull PCM
                            // straight from their own driver's Render()).
                            return loadedSession.RenderSamples(buf, 0, count);
                        });

                    queue = localQueue;
                    localQueue.Start();
                });

                StatusLabel.Text = $"재생 중 - {loadedSession.DescribeActiveChips()}";

                // Poll for completion off the UI thread; only hop back to update labels/buttons.
                CoreAudioQueue? watchedQueue = queue;
                _ = Task.Run(async () =>
                {
                    while (!stopRequested && watchedQueue is { Finished: false })
                    {
                        await Task.Delay(100);
                    }
                    if (!stopRequested)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            StatusLabel.Text = "재생 완료";
                            OpenButton.IsEnabled = true;
                            PlayButton.IsEnabled = true;
                            StopButton.IsEnabled = false;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"오류: {ex.Message}";
                OpenButton.IsEnabled = true;
                PlayButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }

        private void OnStopClick(object? sender, RoutedEventArgs e)
        {
            StopPlayback();
            StatusLabel.Text = "정지됨";
            OpenButton.IsEnabled = true;
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void StopPlayback()
        {
            stopRequested = true;
            queue?.Stop();
            queue?.Dispose();
            queue = null;
        }
    }
}
