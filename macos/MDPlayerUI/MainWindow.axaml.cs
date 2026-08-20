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
        private Ym2413Visualizer? ym2413Visualizer;
        private Ym3526Visualizer? ym3526Visualizer;
        private Ym3812Visualizer? ym3812Visualizer;
        private Y8950Visualizer? y8950Visualizer;
        private Ymf262Visualizer? ymf262Visualizer;
        private Ymf278bVisualizer? ymf278bVisualizer;
        private Ym2203Visualizer? ym2203Visualizer;
        private Ym2608Visualizer? ym2608Visualizer;
        private Ym2609Visualizer? ym2609Visualizer;
        private Ym2610Visualizer? ym2610Visualizer;
        private Ymf271Visualizer? ymf271Visualizer;
        private NesdmcVisualizer? nesdmcVisualizer;
        private FdsVisualizer? fdsVisualizer;
        private Mmc5Visualizer? mmc5Visualizer;
        private Vrc6Visualizer? vrc6Visualizer;
        private Vrc7Visualizer? vrc7Visualizer;
        private N106Visualizer? n106Visualizer;
        private DmgVisualizer? dmgVisualizer;
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

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2413, out uint ym2413Clock))
            {
                ym2413Visualizer = new Ym2413Visualizer(session.ChipRegister, ym2413Clock);
                VisualizerHost.Children.Add(ym2413Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM3526, out uint ym3526Clock))
            {
                ym3526Visualizer = new Ym3526Visualizer(session.ChipRegister, ym3526Clock);
                VisualizerHost.Children.Add(ym3526Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM3812, out uint ym3812Clock))
            {
                ym3812Visualizer = new Ym3812Visualizer(session.ChipRegister, ym3812Clock);
                VisualizerHost.Children.Add(ym3812Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.Y8950, out uint y8950Clock))
            {
                y8950Visualizer = new Y8950Visualizer(session.ChipRegister, y8950Clock);
                VisualizerHost.Children.Add(y8950Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMF262, out uint ymf262Clock))
            {
                ymf262Visualizer = new Ymf262Visualizer(session.ChipRegister, ymf262Clock);
                VisualizerHost.Children.Add(ymf262Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMF278B, out uint ymf278bClock))
            {
                ymf278bVisualizer = new Ymf278bVisualizer(session.ChipRegister, ymf278bClock);
                VisualizerHost.Children.Add(ymf278bVisualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2203, out uint ym2203Clock))
            {
                ym2203Visualizer = new Ym2203Visualizer(session.ChipRegister, ym2203Clock);
                VisualizerHost.Children.Add(ym2203Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2608, out uint ym2608Clock))
            {
                ym2608Visualizer = new Ym2608Visualizer(session.ChipRegister, ym2608Clock);
                VisualizerHost.Children.Add(ym2608Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2609, out uint ym2609Clock))
            {
                ym2609Visualizer = new Ym2609Visualizer(session.ChipRegister, ym2609Clock);
                VisualizerHost.Children.Add(ym2609Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2610, out uint ym2610Clock))
            {
                ym2610Visualizer = new Ym2610Visualizer(session.ChipRegister, ym2610Clock);
                VisualizerHost.Children.Add(ym2610Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMF271, out uint ymf271Clock))
            {
                ymf271Visualizer = new Ymf271Visualizer(session.ChipRegister, ymf271Clock);
                VisualizerHost.Children.Add(ymf271Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.Nes, out uint nesdmcClock))
            {
                nesdmcVisualizer = new NesdmcVisualizer(session.ChipRegister, nesdmcClock);
                VisualizerHost.Children.Add(nesdmcVisualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.FDS, out uint fdsClock))
            {
                fdsVisualizer = new FdsVisualizer(session.ChipRegister, fdsClock);
                VisualizerHost.Children.Add(fdsVisualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.MMC5, out uint mmc5Clock))
            {
                mmc5Visualizer = new Mmc5Visualizer(session.ChipRegister, mmc5Clock);
                VisualizerHost.Children.Add(mmc5Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.VRC6, out uint vrc6Clock))
            {
                vrc6Visualizer = new Vrc6Visualizer(session.ChipRegister, vrc6Clock);
                VisualizerHost.Children.Add(vrc6Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.VRC7, out uint vrc7Clock))
            {
                vrc7Visualizer = new Vrc7Visualizer(session.ChipRegister, vrc7Clock);
                VisualizerHost.Children.Add(vrc7Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.N160, out uint n106Clock))
            {
                n106Visualizer = new N106Visualizer(session.ChipRegister, n106Clock);
                VisualizerHost.Children.Add(n106Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.DMG, out uint dmgClock))
            {
                dmgVisualizer = new DmgVisualizer(session.ChipRegister, dmgClock);
                VisualizerHost.Children.Add(dmgVisualizer.Screen);
            }

            if (sn76489Visualizer == null && ym2612Visualizer == null && ym2151Visualizer == null
                && ay8910Visualizer == null && s5bVisualizer == null && ym2413Visualizer == null
                && ym3526Visualizer == null && ym3812Visualizer == null && y8950Visualizer == null
                && ymf262Visualizer == null && ymf278bVisualizer == null && ym2203Visualizer == null
                && ym2608Visualizer == null && ym2609Visualizer == null && ym2610Visualizer == null
                && ymf271Visualizer == null && nesdmcVisualizer == null && fdsVisualizer == null
                && mmc5Visualizer == null && vrc6Visualizer == null && vrc7Visualizer == null
                && n106Visualizer == null && dmgVisualizer == null) return;

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
                ym2413Visualizer?.ScreenChangeParams();
                ym2413Visualizer?.ScreenDrawParams();
                ym3526Visualizer?.ScreenChangeParams();
                ym3526Visualizer?.ScreenDrawParams();
                ym3812Visualizer?.ScreenChangeParams();
                ym3812Visualizer?.ScreenDrawParams();
                y8950Visualizer?.ScreenChangeParams();
                y8950Visualizer?.ScreenDrawParams();
                ymf262Visualizer?.ScreenChangeParams();
                ymf262Visualizer?.ScreenDrawParams();
                ymf278bVisualizer?.ScreenChangeParams();
                ymf278bVisualizer?.ScreenDrawParams();
                ym2203Visualizer?.ScreenChangeParams();
                ym2203Visualizer?.ScreenDrawParams();
                ym2608Visualizer?.ScreenChangeParams();
                ym2608Visualizer?.ScreenDrawParams();
                ym2609Visualizer?.ScreenChangeParams();
                ym2609Visualizer?.ScreenDrawParams();
                ym2610Visualizer?.ScreenChangeParams();
                ym2610Visualizer?.ScreenDrawParams();
                ymf271Visualizer?.ScreenChangeParams();
                ymf271Visualizer?.ScreenDrawParams();
                nesdmcVisualizer?.ScreenChangeParams();
                nesdmcVisualizer?.ScreenDrawParams();
                fdsVisualizer?.ScreenChangeParams();
                fdsVisualizer?.ScreenDrawParams();
                mmc5Visualizer?.ScreenChangeParams();
                mmc5Visualizer?.ScreenDrawParams();
                vrc6Visualizer?.ScreenChangeParams();
                vrc6Visualizer?.ScreenDrawParams();
                vrc7Visualizer?.ScreenChangeParams();
                vrc7Visualizer?.ScreenDrawParams();
                n106Visualizer?.ScreenChangeParams();
                n106Visualizer?.ScreenDrawParams();
                dmgVisualizer?.ScreenChangeParams();
                dmgVisualizer?.ScreenDrawParams();
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
            ym2413Visualizer = null;
            ym3526Visualizer = null;
            ym3812Visualizer = null;
            y8950Visualizer = null;
            ymf262Visualizer = null;
            ymf278bVisualizer = null;
            ym2203Visualizer = null;
            ym2608Visualizer = null;
            ym2609Visualizer = null;
            ym2610Visualizer = null;
            ymf271Visualizer = null;
            nesdmcVisualizer = null;
            fdsVisualizer = null;
            mmc5Visualizer = null;
            vrc6Visualizer = null;
            vrc7Visualizer = null;
            n106Visualizer = null;
            dmgVisualizer = null;
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
