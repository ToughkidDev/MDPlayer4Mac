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
using MDPlayer.UI.Visualizer;

namespace MDPlayer.UI
{
    public partial class MainWindow : Window
    {
        private const int FramesPerBuffer = 2048;
        private const int BufferCount = 4;

        // ~30fps - lighter than the original Windows app's default 60fps
        // (Setting.other.ScreenFrameRate, frmMain.cs's screenMainLoop), plenty smooth for
        // LED-bar/piano-key style content and one less thing to tune blind before a real
        // build. Easy to raise once this is confirmed working on the real Mac.
        private static readonly TimeSpan VisualizerInterval = TimeSpan.FromMilliseconds(33);

        private byte[]? loadedVgmBytes;
        private string? loadedFileName;
        private CoreAudioQueue? queue;
        private volatile bool stopRequested;

        // Retained across the play flow (previously only a local in OnPlayClick) so the
        // visualizer redraw timer can keep polling ChipRegister/ChipClocks for as long as
        // playback runs.
        private MusicEngineSession? loadedSession;
        private Sn76489Visualizer? sn76489Visualizer;
        private Ym2612Visualizer? ym2612Visualizer;
        private Ym2151Visualizer? ym2151Visualizer;
        private Ay8910Visualizer? ay8910Visualizer;
        private S5bVisualizer? s5bVisualizer;
        private DispatcherTimer? visualizerTimer;

        public MainWindow()
        {
            InitializeComponent();
            Closing += (_, _) =>
            {
                StopPlayback();
                HideVisualizers();
            };
        }

        // Builds whichever chip visualizers this session's ChipClocks says are present and
        // docks them into VisualizerHost, then starts the shared redraw timer. Confirmed
        // working live on real hardware (see macos/README.md's chip-visualizer section) -
        // the earlier "frozen SN76489 UI" report turned out not to be a rendering bug (a
        // temporary debug build that dumped raw ChipRegister.sn76489Register/GetPSGVolume
        // state alongside the tick counter showed the register values themselves changing
        // and the LED bars/keyboard animating correctly in step - the confusion was just
        // that the same session also had no YM2612 UI yet, which looked like "nothing is
        // reacting" at a glance). That diagnostic instrumentation has been removed now that
        // it's served its purpose.
        private void ShowVisualizersFor(MusicEngineSession session)
        {
            HideVisualizers();

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.SN76489, out uint sn76489Clock))
            {
                sn76489Visualizer = new Sn76489Visualizer(session.ChipRegister, sn76489Clock);
                VisualizerHost.Children.Add(sn76489Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2612, out uint ym2612Clock))
            {
                ym2612Visualizer = new Ym2612Visualizer(session.ChipRegister, ym2612Clock);
                VisualizerHost.Children.Add(ym2612Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2151, out uint ym2151Clock))
            {
                ym2151Visualizer = new Ym2151Visualizer(session.ChipRegister, ym2151Clock);
                VisualizerHost.Children.Add(ym2151Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.AY8910, out uint ay8910Clock))
            {
                ay8910Visualizer = new Ay8910Visualizer(session.ChipRegister, ay8910Clock);
                VisualizerHost.Children.Add(ay8910Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.FME7, out uint s5bClock))
            {
                s5bVisualizer = new S5bVisualizer(session.ChipRegister, s5bClock);
                VisualizerHost.Children.Add(s5bVisualizer.Screen);
            }

            if (sn76489Visualizer == null && ym2612Visualizer == null && ym2151Visualizer == null
                && ay8910Visualizer == null && s5bVisualizer == null) return;

            visualizerTimer = new DispatcherTimer { Interval = VisualizerInterval };
            visualizerTimer.Tick += (_, _) =>
            {
                sn76489Visualizer?.ScreenChangeParams();
                sn76489Visualizer?.ScreenDrawParams();
                ym2612Visualizer?.ScreenChangeParams();
                ym2612Visualizer?.ScreenDrawParams();
                ym2151Visualizer?.ScreenChangeParams();
                ym2151Visualizer?.ScreenDrawParams();
                ay8910Visualizer?.ScreenChangeParams();
                ay8910Visualizer?.ScreenDrawParams();
                s5bVisualizer?.ScreenChangeParams();
                s5bVisualizer?.ScreenDrawParams();
            };
            visualizerTimer.Start();
        }

        private void HideVisualizers()
        {
            visualizerTimer?.Stop();
            visualizerTimer = null;
            sn76489Visualizer = null;
            ym2612Visualizer = null;
            ym2151Visualizer = null;
            ay8910Visualizer = null;
            s5bVisualizer = null;
            VisualizerHost.Children.Clear();
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
            HideVisualizers();

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
                loadedSession = await Task.Run(() => MusicEngine.Load(vgmBuf, fileName));
                MusicEngineSession? session = loadedSession;
                if (session == null || stopRequested)
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
                        session.SampleRate, FramesPerBuffer, BufferCount,
                        (buf, count) =>
                        {
                            if (stopRequested || session.Driver.Stopped) return 0;
                            // loadedSession.RenderSamples, not Mds.Update() directly - see
                            // EngineSmokeTest/Program.cs's identical comment (SID/NSF/MDX
                            // bypass MDSound.MDSound.Chip.Update() entirely and pull PCM
                            // straight from their own driver's Render()).
                            return session.RenderSamples(buf, 0, count);
                        });

                    queue = localQueue;
                    localQueue.Start();
                });

                StatusLabel.Text = $"재생 중 - {session.DescribeActiveChips()}";
                ShowVisualizersFor(session);

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
                            HideVisualizers();
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
                HideVisualizers();
            }
        }

        private void OnStopClick(object? sender, RoutedEventArgs e)
        {
            StopPlayback();
            StatusLabel.Text = "정지됨";
            OpenButton.IsEnabled = true;
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            HideVisualizers();
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
