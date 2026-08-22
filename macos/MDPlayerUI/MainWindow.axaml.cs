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
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MDPlayer;
using MDPlayer.CoreAudioOutput;
using MDPlayer.UI.Visualizer;

namespace MDPlayer.UI
{
    public partial class MainWindow : Window
    {
        private const int FramesPerBuffer = 2048;
        private const int BufferCount = 4;
        private const double CompactPlaylistRowHeight = 18;
        private const double EmbeddedPlaylistRows = 3.5;
        private const double PlaylistViewRows = 20;

        private enum ActiveViewMode { Channel, Volume, Playlist }

        // ~30fps - lighter than the original Windows app's default 60fps
        // (Setting.other.ScreenFrameRate, frmMain.cs's screenMainLoop), plenty smooth for
        // LED-bar/piano-key style content and one less thing to tune blind before a real
        // build. Easy to raise once this is confirmed working on the real Mac.
        private static readonly TimeSpan VisualizerInterval = TimeSpan.FromMilliseconds(33);

        private byte[]? loadedVgmBytes;
        private string? loadedFileName;
        private CoreAudioQueue? queue;
        private volatile bool stopRequested;
        private bool isPaused;
        private bool isStarting;
        private bool playbackEnded;
        private bool loopEnabled;
        private bool autoPlayEnabled;
        private long fileLoopCounter;
        private string? channelLayoutSignature;
        private ActiveViewMode activeViewMode = ActiveViewMode.Channel;
        // Channel screens are created at the Windows-compatible 2x display scale. The
        // dashboard Zoom button toggles the complete channel stack to its native 1x size.
        private bool channelViewHalfSize;
        // Captured from the channel screen after it has completed a layout pass. Every
        // dashboard mode keeps at least this width, so changing to the narrower playlist
        // or mixer content cannot make the player window collapse below the channel view.
        private double channelViewMinimumWidth;

        // SourcePath is retained separately from the display name so formats such as MDX
        // can resolve their companion PDX bank in the same directory after being selected
        // again from the playlist or restarted by the Play button.
        private sealed record PlaylistEntry(byte[] Bytes, string Name, string? SourcePath);
        private readonly System.Collections.Generic.List<PlaylistEntry> playlist = new();
        private int playlistIndex = -1;
        private bool synchronizingPlaylistSelection;
        private ListBox? focusedPlaylistList;
        private KeyEventArgs? lastHandledPlaylistKeyEvent;

        // Built from the Windows frmMain cc/ch/ci sprite triplets after XAML has created
        // the two host rows. The transport row is Stop, Pause, Previous, Slow, Play, Fast,
        // Next; the utility row contains Open, the mixer and the channel keyboard view.
        private TransportSpriteButton openButton = null!;
        private TransportSpriteButton stopButton = null!;
        private TransportSpriteButton pauseButton = null!;
        private TransportSpriteButton previousButton = null!;
        private TransportSpriteButton slowButton = null!;
        private TransportSpriteButton playButton = null!;
        private TransportSpriteButton fastButton = null!;
        private TransportSpriteButton nextButton = null!;
        private TransportSpriteButton playlistViewButton = null!;
        private TransportSpriteButton volumeViewButton = null!;
        private TransportSpriteButton channelViewButton = null!;
        private TransportSpriteButton zoomButton = null!;
        private TransportSpriteButton loopButton = null!;

        // Kept independently of a playback session so a volume change made for one song is
        // immediately honoured when the user opens another song.  The current Setting is
        // persisted on application close; the mixer itself deliberately has no extra text
        // controls or save/reset buttons.
        private readonly System.Collections.Generic.Dictionary<ChipVolumeKey, int> chipVolumeOverrides = new();
        private int? masterVolumeOverride;

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
        private Huc6280Visualizer? huc6280Visualizer;
        private K051649Visualizer? k051649Visualizer;
        private C140Visualizer? c140Visualizer;
        private C352Visualizer? c352Visualizer;
        private GA20Visualizer? ga20Visualizer;
        private K053260Visualizer? k053260Visualizer;
        private K054539Visualizer? k054539Visualizer;
        private MegaCDVisualizer? megaCdVisualizer;
        private MpcmX68kVisualizer? mpcmX68kVisualizer;
        private MultiPCMVisualizer? multiPcmVisualizer;
        private OKIM6258Visualizer? okim6258Visualizer;
        private OKIM6295Visualizer? okim6295Visualizer;
        private PCM8Visualizer? pcm8Visualizer;
        private QSoundVisualizer? qSoundVisualizer;
        private Rf5c68Visualizer? rf5c68Visualizer;
        private SegaPcmVisualizer? segaPcmVisualizer;
        private Ymz280BVisualizer? ymz280BVisualizer;

        // Holds the *second* chip instance's visualizer for any dual-chip VGM (chipID: 1) -
        // see ShowVisualizersFor's `vgm.XxxDualChipFlag` checks. Generic over
        // IChannelVisualizer rather than one more named field per chip type, since there's
        // nothing chip-specific left to do with these once constructed: dock the Screen,
        // and pump ScreenChangeParams/ScreenDrawParams on every tick, same as any primary
        // visualizer.
        private readonly System.Collections.Generic.List<IChannelVisualizer> secondaryVisualizers = new();
        private readonly StackPanel VisualizerHost = new() { Spacing = 4 };

        private void AddSecondaryVisualizer(IChannelVisualizer visualizer)
        {
            secondaryVisualizers.Add(visualizer);
            VisualizerHost.Children.Add(visualizer.Screen);
        }

        private DispatcherTimer? visualizerTimer;
        private MixerVisualizer? mixerVisualizer;
        private readonly StackPanel volumeViewHost = new() { Spacing = 8 };
        private readonly Button resetVolumeButton = new() { Content = "볼륨 리셋" };
        private readonly DockPanel playlistViewHost = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        private readonly ListBox playlistViewList = new()
        {
            // A playlist-only window is always a 20-row viewer. More tracks scroll inside
            // the ListBox instead of increasing the outer window's desired height.
            Height = CompactPlaylistRowHeight * PlaylistViewRows,
            MinHeight = CompactPlaylistRowHeight * PlaylistViewRows,
            MaxHeight = CompactPlaylistRowHeight * PlaylistViewRows,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            SelectionMode = SelectionMode.Multiple,
        };

        private void ApplyChipVolumeOverrides(MusicEngineSession session)
        {
            foreach (var overrideEntry in chipVolumeOverrides)
            {
                if (session.ChipVolumeSlots.Exists(slot => slot.Key == overrideEntry.Key))
                {
                    session.SetChipVolume(overrideEntry.Key, overrideEntry.Value);
                }
            }
            if (masterVolumeOverride is int masterVolume)
            {
                session.SetMasterVolume(masterVolume);
            }
        }

        private void ShowMixerFor(MusicEngineSession session)
        {
            mixerVisualizer = new MixerVisualizer(session);
            mixerVisualizer.ChipVolumeChanged += (chipKey, volume) => chipVolumeOverrides[chipKey] = volume;
            mixerVisualizer.MasterVolumeChanged += volume => masterVolumeOverride = volume;
            volumeViewButton.IsEnabled = true;
            channelViewButton.IsEnabled = true;
        }

        private void HideMixer()
        {
            mixerVisualizer = null;
            volumeViewButton.IsEnabled = false;
            channelViewButton.IsEnabled = false;
            volumeViewButton.IsSelected = false;
            channelViewButton.IsSelected = false;
            playlistViewButton.IsSelected = false;
            SetPlaylistPanelVisibility(playlistOnly: false);
            ViewHost.Content = null;
            volumeViewHost.Children.Clear();
        }

        private void ShowVolumeView(bool refitWindow = true)
        {
            if (mixerVisualizer == null) return;
            MaxHeight = double.PositiveInfinity;
            activeViewMode = ActiveViewMode.Volume;
            SetEmbeddedPlaylistSize();
            SetPlaylistPanelVisibility(playlistOnly: false);
            mixerVisualizer.FitToChannelViewWidth(VisualizerHost.Bounds.Width);
            volumeViewHost.Children.Clear();
            volumeViewHost.Children.Add(mixerVisualizer.Screen);
            volumeViewHost.Children.Add(resetVolumeButton);
            ViewHost.Content = volumeViewHost;
            volumeViewButton.IsSelected = true;
            channelViewButton.IsSelected = false;
            playlistViewButton.IsSelected = false;
            volumeViewButton.IsEnabled = true;
            channelViewButton.IsEnabled = true;
            UpdateTransportButtons();
            if (refitWindow) RefitWindowToActiveView();
        }

        private void ShowChannelView(bool refitWindow = true)
        {
            MaxHeight = double.PositiveInfinity;
            activeViewMode = ActiveViewMode.Channel;
            SetEmbeddedPlaylistSize();
            SetPlaylistPanelVisibility(playlistOnly: false);
            ViewHost.Content = VisualizerHost;
            volumeViewButton.IsSelected = false;
            channelViewButton.IsSelected = true;
            playlistViewButton.IsSelected = false;
            volumeViewButton.IsEnabled = mixerVisualizer != null;
            channelViewButton.IsEnabled = mixerVisualizer != null;
            UpdateTransportButtons();
            if (refitWindow) RefitWindowToActiveView();
        }

        private void ShowPlaylistView(bool refitWindow = true)
        {
            if (playlist.Count == 0) return;
            // Let SizeToContent measure the fixed 20-row ListBox first. The matching
            // MinHeight/MaxHeight lock is applied in UpdateActiveViewMinimumSize.
            if (refitWindow) MaxHeight = double.PositiveInfinity;
            activeViewMode = ActiveViewMode.Playlist;
            SetPlaylistPanelVisibility(playlistOnly: true);
            ViewHost.Content = playlistViewHost;
            volumeViewButton.IsSelected = false;
            channelViewButton.IsSelected = false;
            playlistViewButton.IsSelected = true;
            UpdateTransportButtons();
            if (refitWindow) RefitWindowToActiveView();
        }

        private void RestoreActiveView(bool refitChannelWindow = true)
        {
            switch (activeViewMode)
            {
                case ActiveViewMode.Volume when mixerVisualizer != null:
                    ShowVolumeView(refitWindow: false);
                    break;
                case ActiveViewMode.Playlist when playlist.Count > 0:
                    ShowPlaylistView(refitWindow: false);
                    break;
                default:
                    ShowChannelView(refitChannelWindow);
                    break;
            }
        }

        private void SetEmbeddedPlaylistSize()
            => PlaylistList.Height = CompactPlaylistRowHeight * EmbeddedPlaylistRows;

        private void ToggleChannelViewSize()
        {
            channelViewHalfSize = !channelViewHalfSize;
            ApplyChannelViewScale();
            zoomButton.IsSelected = channelViewHalfSize;
            StatusLabel.Text = channelViewHalfSize ? "채널 뷰 50% 크기" : "채널 뷰 원래 크기";

            // Only the channel view's desired size changes. Mixer/playlist modes retain
            // their own layout until the user explicitly returns to the channel view.
            if (activeViewMode == ActiveViewMode.Channel)
            {
                RefitWindowToActiveView();
            }
        }

        private void ApplyChannelViewScale()
        {
            double scale = channelViewHalfSize ? 1.0 : 2.0;
            foreach (PixelScreen screen in VisualizerHost.Children.OfType<PixelScreen>())
            {
                screen.SetDisplayScale(scale);
            }
        }

        private void SetPlaylistPanelVisibility(bool playlistOnly)
        {
            // Normal channel/mixer modes content-size the final row to the compact 3.5-row
            // playlist. In playlist-only mode that same row becomes a star row: ActiveViewPanel
            // and its DockPanel/ListBox then stretch all the way to the main-window bottom.
            PlaylistPanel.IsVisible = playlist.Count > 0 && !playlistOnly;
            Grid.SetRow(ActiveViewPanel, playlistOnly ? 4 : 3);
            MainLayout.RowDefinitions[4].Height = playlistOnly
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
        }

        // Refit after replacing ViewHost's child. The chip screens report their own desired
        // pixel sizes only after a layout pass, so let Avalonia perform that unconstrained
        // content measurement instead of assigning ClientSize from a stale, clipped measure.
        private void RefitWindowToActiveView()
        {
            MinWidth = 0;
            MinHeight = 0;
            SizeToContent = Avalonia.Controls.SizeToContent.Manual;
            Dispatcher.UIThread.Post(() =>
            {
                InvalidateMeasure();
                InvalidateArrange();
                SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight;
                // Capture the post-measure minimum only after SizeToContent has allowed the
                // active view and its playlist panel to arrange.
                Dispatcher.UIThread.Post(UpdateActiveViewMinimumSize, DispatcherPriority.Render);
            }, DispatcherPriority.Render);
        }

        private void UpdateActiveViewMinimumSize()
        {
            if (ActiveViewPanel.Bounds.Width <= 0 || ActiveViewPanel.Bounds.Height <= 0) return;

            // ActiveViewPanel includes its border/padding. The remainder covers the
            // dashboard, outer margins and native window chrome.
            double windowChromeWidth = Math.Max(0, Bounds.Width - ActiveViewPanel.Bounds.Width);
            double activeViewWidth = ActiveViewPanel.DesiredSize.Width;

            // A channel view defines the player's canonical minimum width. Remember it so
            // the volume and playlist-only views cannot be shrunk narrower afterwards.
            if (ReferenceEquals(ViewHost.Content, VisualizerHost) && activeViewWidth > 0)
            {
                channelViewMinimumWidth = Math.Ceiling(activeViewWidth + windowChromeWidth);
            }

            double activeViewMinimumWidth = Math.Ceiling(activeViewWidth + windowChromeWidth);
            MinWidth = Math.Max(channelViewMinimumWidth, activeViewMinimumWidth);

            // Each mode has a different essential view height (channel stack, all mixer
            // faders, or the 20-row playlist). Lock the minimum to the post-fit desired
            // height so the active content cannot disappear below the window bottom.
            double nonViewHeight = Math.Max(0, Bounds.Height - ActiveViewPanel.Bounds.Height);
            double activeMinimumHeight = Math.Ceiling(nonViewHeight + ActiveViewPanel.DesiredSize.Height);
            MinHeight = activeMinimumHeight;

            // Playlist-only mode is deliberately a fixed 20-row window, not merely a
            // fixed-height ListBox. Extra tracks use the ListBox's internal scrollbar and
            // cannot make the outer window taller.
            MaxHeight = activeViewMode == ActiveViewMode.Playlist
                ? activeMinimumHeight
                : double.PositiveInfinity;
        }

        public MainWindow()
        {
            InitializeComponent();
            var playlistViewTitle = new TextBlock { Text = "재생 목록" };
            DockPanel.SetDock(playlistViewTitle, Dock.Top);
            playlistViewHost.Children.Add(playlistViewTitle);
            playlistViewHost.Children.Add(playlistViewList);
            // Styles have a single owner collection in Avalonia, so build a separate compact
            // style rather than reusing PlaylistList.Styles (which would abort at startup).
            var compactPlaylistStyle = new Style(selector => selector.OfType<ListBoxItem>());
            compactPlaylistStyle.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(6, 1)));
            compactPlaylistStyle.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0d));
            playlistViewList.Styles.Add(compactPlaylistStyle);
            ScrollViewer.SetVerticalScrollBarVisibility(playlistViewList, ScrollBarVisibility.Auto);
            playlistViewList.SelectionChanged += OnPlaylistViewSelectionChanged;
            playlistViewList.DoubleTapped += OnPlaylistViewDoubleTapped;
            PlaylistList.GotFocus += OnPlaylistGotFocus;
            playlistViewList.GotFocus += OnPlaylistGotFocus;
            PlaylistList.PointerPressed += OnPlaylistPointerPressed;
            playlistViewList.PointerPressed += OnPlaylistPointerPressed;
            // Avalonia's macOS backend can route a key through either tunnel or bubble
            // depending on whether a ListBoxItem owns the native focus. Observe both,
            // including already-handled keys; the event-instance guard below prevents a
            // single physical key press from being applied twice.
            AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            // The full playlist-only view is created in code rather than XAML, so wire the
            // same append target here as the compact playlist panel.
            DragDrop.SetAllowDrop(playlistViewHost, true);
            DragDrop.AddDragEnterHandler(playlistViewHost, OnPlaylistDragEnter);
            DragDrop.AddDragOverHandler(playlistViewHost, OnPlaylistDragOver);
            DragDrop.AddDragLeaveHandler(playlistViewHost, OnPlaylistDragLeave);
            DragDrop.AddDropHandler(playlistViewHost, OnPlaylistDrop);
            DragDrop.AddDragEnterHandler(PlaylistPanel, OnPlaylistDragEnter);
            DragDrop.AddDragOverHandler(PlaylistPanel, OnPlaylistDragOver);
            DragDrop.AddDragLeaveHandler(PlaylistPanel, OnPlaylistDragLeave);
            DragDrop.AddDropHandler(PlaylistPanel, OnPlaylistDrop);
            DragDrop.AddDragEnterHandler(PlayerDashboard, OnDashboardDragEnter);
            DragDrop.AddDragOverHandler(PlayerDashboard, OnDashboardDragOver);
            DragDrop.AddDragLeaveHandler(PlayerDashboard, OnDashboardDragLeave);
            DragDrop.AddDropHandler(PlayerDashboard, OnDashboardDrop);
            BuildTransportButtons();
            DragDrop.AddDragOverHandler(this, OnFileDragOver);
            DragDrop.AddDragEnterHandler(this, OnFileDragEnter);
            DragDrop.AddDragLeaveHandler(this, OnFileDragLeave);
            DragDrop.AddDropHandler(this, OnFileDrop);
            resetVolumeButton.Click += OnResetVolumeClick;
            Closing += (_, _) =>
            {
                StopPlayback();
                HideVisualizers();
                try { loadedSession?.Setting.Save(); } catch { }
                HideMixer();
            };
        }

        private void BuildTransportButtons()
        {
            openButton = MakeTransportButton("OpenFolder", "파일 열기", () => OnOpenClick(null, null));
            stopButton = MakeTransportButton("Stop", "정지", () => OnStopClick(null, null));
            pauseButton = MakeTransportButton("Pause", "일시 정지 / 계속", OnPauseClick);
            previousButton = MakeTransportButton("Previous", "이전 곡", OnPreviousClick);
            slowButton = MakeTransportButton("Slow", "느리게 (재생 속도)", () => ChangePlaybackSpeed(0.5));
            playButton = MakeTransportButton("Play", "재생 / 계속 — 2초 누름: 자동 재생", () => OnPlayClick(null, null));
            playButton.LongPressed += ToggleAutoPlay;
            playButton.AllowLongPressWhenDisabled = true;
            playButton.EnableForceTouchLongPress();
            fastButton = MakeTransportButton("Fast", "빠르게 (재생 속도)", () => ChangePlaybackSpeed(2.0));
            nextButton = MakeTransportButton("Next", "다음 곡", OnNextClick);
            playlistViewButton = MakeTransportButton("PlayList", "재생목록 뷰", () => ShowPlaylistView());
            volumeViewButton = MakeTransportButton("Mixer", "볼륨 뷰", () => ShowVolumeView());
            channelViewButton = MakeTransportButton("KBD", "채널 뷰", () => ShowChannelView());
            zoomButton = MakeTransportButton("Zoom", "채널 뷰 50% 크기 / 원래 크기", ToggleChannelViewSize);
            loopButton = MakeTransportButton("Loop", "현재 곡 반복", OnLoopClick);

            TransportButtonsHost.Children.Add(stopButton.Screen);
            TransportButtonsHost.Children.Add(pauseButton.Screen);
            TransportButtonsHost.Children.Add(previousButton.Screen);
            TransportButtonsHost.Children.Add(slowButton.Screen);
            TransportButtonsHost.Children.Add(playButton.Screen);
            TransportButtonsHost.Children.Add(fastButton.Screen);
            TransportButtonsHost.Children.Add(nextButton.Screen);
            UtilityButtonsHost.Children.Add(openButton.Screen);
            UtilityButtonsHost.Children.Add(playlistViewButton.Screen);
            UtilityButtonsHost.Children.Add(volumeViewButton.Screen);
            UtilityButtonsHost.Children.Add(channelViewButton.Screen);
            UtilityButtonsHost.Children.Add(zoomButton.Screen);
            UtilityButtonsHost.Children.Add(loopButton.Screen);
            UpdateTransportButtons();
        }

        private static TransportSpriteButton MakeTransportButton(string icon, string tooltip, Action click)
        {
            TransportSpriteButton button = new(icon, tooltip);
            button.Click += click;
            return button;
        }

        private void UpdateTransportButtons()
        {
            bool hasTrack = playlistIndex >= 0;
            bool playbackActive = queue != null && !playbackEnded;
            bool playing = playbackActive && !isPaused;
            bool canChangeTrack = playlist.Count > 1 && !isStarting;

            openButton.IsEnabled = !isStarting;
            playButton.IsEnabled = hasTrack && !isStarting;
            playlistViewButton.IsEnabled = playlist.Count > 0;
            stopButton.IsEnabled = playbackActive;
            pauseButton.IsEnabled = playbackActive;
            previousButton.IsEnabled = canChangeTrack;
            nextButton.IsEnabled = canChangeTrack;
            slowButton.IsEnabled = playbackActive && !isStarting;
            fastButton.IsEnabled = playbackActive && !isStarting;
            playButton.IsSelected = playing;
            pauseButton.IsSelected = isPaused;
            zoomButton.IsEnabled = activeViewMode == ActiveViewMode.Channel && VisualizerHost.Children.Count > 0;
            zoomButton.IsSelected = channelViewHalfSize;
            loopButton.IsEnabled = true;
            loopButton.IsSelected = loopEnabled;
            playButton.IsRedAlert = autoPlayEnabled;
        }

        private void ToggleAutoPlay()
        {
            autoPlayEnabled = !autoPlayEnabled;
            playButton.IsRedAlert = autoPlayEnabled;
            StatusLabel.Text = autoPlayEnabled ? "자동 재생 켜짐" : "자동 재생 꺼짐";
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

            // VGM headers can flag a chip as present twice (e.g. two SN76489s) via a
            // per-chip DualChipFlag on the Vgm driver - VgmEngine.Load now wires both
            // physical instances into MDSound/ChipRegister when that's set, so the UI
            // mirrors that here: every dual-chip-capable block below additionally opens a
            // second visualizer (chipID: 1) alongside the usual chipID-0 one. Declared once
            // up front (rather than as an `is Vgm vgm` pattern in each check below) because
            // C# would otherwise reject redeclaring the same pattern-variable name multiple
            // times in this method body. Non-VGM formats (nsf/gbs/hes/sid/...) simply never
            // hit any of the dual-chip branches, since `vgm` stays null for them.
            Vgm? vgm = session.Driver as Vgm;

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.SN76489, out uint sn76489Clock))
            {
                sn76489Visualizer = new Sn76489Visualizer(session.ChipRegister, sn76489Clock);
                VisualizerHost.Children.Add(sn76489Visualizer.Screen);
            }

            if (vgm != null && vgm.SN76489DualChipFlag && sn76489Visualizer != null)
            {
                AddSecondaryVisualizer(new Sn76489Visualizer(session.ChipRegister, sn76489Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2612, out uint ym2612Clock))
            {
                ym2612Visualizer = new Ym2612Visualizer(session.ChipRegister, ym2612Clock);
                VisualizerHost.Children.Add(ym2612Visualizer.Screen);
            }

            if (vgm != null && vgm.YM2612DualChipFlag && ym2612Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym2612Visualizer(session.ChipRegister, ym2612Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2151, out uint ym2151Clock))
            {
                ym2151Visualizer = new Ym2151Visualizer(session.ChipRegister, ym2151Clock);
                VisualizerHost.Children.Add(ym2151Visualizer.Screen);
            }

            if (vgm != null && vgm.YM2151DualChipFlag && ym2151Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym2151Visualizer(session.ChipRegister, ym2151Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.AY8910, out uint ay8910Clock))
            {
                ay8910Visualizer = new Ay8910Visualizer(session.ChipRegister, ay8910Clock);
                VisualizerHost.Children.Add(ay8910Visualizer.Screen);
            }

            if (vgm != null && vgm.AY8910DualChipFlag && ay8910Visualizer != null)
            {
                AddSecondaryVisualizer(new Ay8910Visualizer(session.ChipRegister, ay8910Clock, chipID: 1));
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

            if (vgm != null && vgm.YM2413DualChipFlag && ym2413Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym2413Visualizer(session.ChipRegister, ym2413Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM3526, out uint ym3526Clock))
            {
                ym3526Visualizer = new Ym3526Visualizer(session.ChipRegister, ym3526Clock);
                VisualizerHost.Children.Add(ym3526Visualizer.Screen);
            }

            if (vgm != null && vgm.YM3526DualChipFlag && ym3526Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym3526Visualizer(session.ChipRegister, ym3526Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM3812, out uint ym3812Clock))
            {
                ym3812Visualizer = new Ym3812Visualizer(session.ChipRegister, ym3812Clock);
                VisualizerHost.Children.Add(ym3812Visualizer.Screen);
            }

            if (vgm != null && vgm.YM3812DualChipFlag && ym3812Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym3812Visualizer(session.ChipRegister, ym3812Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.Y8950, out uint y8950Clock))
            {
                y8950Visualizer = new Y8950Visualizer(session.ChipRegister, y8950Clock);
                VisualizerHost.Children.Add(y8950Visualizer.Screen);
            }

            if (vgm != null && vgm.Y8950DualChipFlag && y8950Visualizer != null)
            {
                AddSecondaryVisualizer(new Y8950Visualizer(session.ChipRegister, y8950Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMF262, out uint ymf262Clock))
            {
                ymf262Visualizer = new Ymf262Visualizer(session.ChipRegister, ymf262Clock);
                VisualizerHost.Children.Add(ymf262Visualizer.Screen);
            }

            if (vgm != null && vgm.YMF262DualChipFlag && ymf262Visualizer != null)
            {
                AddSecondaryVisualizer(new Ymf262Visualizer(session.ChipRegister, ymf262Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMF278B, out uint ymf278bClock))
            {
                ymf278bVisualizer = new Ymf278bVisualizer(session.ChipRegister, ymf278bClock);
                VisualizerHost.Children.Add(ymf278bVisualizer.Screen);
            }

            if (vgm != null && vgm.YMF278BDualChipFlag && ymf278bVisualizer != null)
            {
                AddSecondaryVisualizer(new Ymf278bVisualizer(session.ChipRegister, ymf278bClock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2203, out uint ym2203Clock))
            {
                ym2203Visualizer = new Ym2203Visualizer(session.ChipRegister, ym2203Clock);
                VisualizerHost.Children.Add(ym2203Visualizer.Screen);
            }

            if (vgm != null && vgm.YM2203DualChipFlag && ym2203Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym2203Visualizer(session.ChipRegister, ym2203Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YM2608, out uint ym2608Clock))
            {
                ym2608Visualizer = new Ym2608Visualizer(session.ChipRegister, ym2608Clock);
                VisualizerHost.Children.Add(ym2608Visualizer.Screen);
            }

            if (vgm != null && vgm.YM2608DualChipFlag && ym2608Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym2608Visualizer(session.ChipRegister, ym2608Clock, chipID: 1));
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

            if (vgm != null && vgm.YM2610DualChipFlag && ym2610Visualizer != null)
            {
                AddSecondaryVisualizer(new Ym2610Visualizer(session.ChipRegister, ym2610Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMF271, out uint ymf271Clock))
            {
                ymf271Visualizer = new Ymf271Visualizer(session.ChipRegister, ymf271Clock);
                VisualizerHost.Children.Add(ymf271Visualizer.Screen);
            }

            if (vgm != null && vgm.YMF271DualChipFlag && ymf271Visualizer != null)
            {
                AddSecondaryVisualizer(new Ymf271Visualizer(session.ChipRegister, ymf271Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.Nes, out uint nesdmcClock))
            {
                nesdmcVisualizer = new NesdmcVisualizer(session.ChipRegister, nesdmcClock);
                VisualizerHost.Children.Add(nesdmcVisualizer.Screen);
            }

            if (vgm != null && vgm.NESDualChipFlag && nesdmcVisualizer != null)
            {
                AddSecondaryVisualizer(new NesdmcVisualizer(session.ChipRegister, nesdmcClock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.FDS, out uint fdsClock))
            {
                fdsVisualizer = new FdsVisualizer(session.ChipRegister, fdsClock);
                VisualizerHost.Children.Add(fdsVisualizer.Screen);
            }

            if (vgm != null && vgm.NESDualChipFlag && fdsVisualizer != null)
            {
                AddSecondaryVisualizer(new FdsVisualizer(session.ChipRegister, fdsClock, chipID: 1));
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

            if (vgm != null && vgm.DMGDualChipFlag && dmgVisualizer != null)
            {
                AddSecondaryVisualizer(new DmgVisualizer(session.ChipRegister, dmgClock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.HuC6280, out uint huc6280Clock))
            {
                huc6280Visualizer = new Huc6280Visualizer(session.ChipRegister, huc6280Clock);
                VisualizerHost.Children.Add(huc6280Visualizer.Screen);
            }

            if (vgm != null && vgm.HuC6280DualChipFlag && huc6280Visualizer != null)
            {
                AddSecondaryVisualizer(new Huc6280Visualizer(session.ChipRegister, huc6280Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.K051649, out uint k051649Clock))
            {
                k051649Visualizer = new K051649Visualizer(session.ChipRegister, k051649Clock);
                VisualizerHost.Children.Add(k051649Visualizer.Screen);
            }

            if (vgm != null && vgm.K051649DualChipFlag && k051649Visualizer != null)
            {
                AddSecondaryVisualizer(new K051649Visualizer(session.ChipRegister, k051649Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.C140, out uint c140Clock))
            {
                c140Visualizer = new C140Visualizer(session.ChipRegister, c140Clock);
                VisualizerHost.Children.Add(c140Visualizer.Screen);
            }

            if (vgm != null && vgm.C140DualChipFlag && c140Visualizer != null)
            {
                AddSecondaryVisualizer(new C140Visualizer(session.ChipRegister, c140Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.C352, out uint c352Clock))
            {
                c352Visualizer = new C352Visualizer(session.ChipRegister, c352Clock);
                VisualizerHost.Children.Add(c352Visualizer.Screen);
            }

            if (vgm != null && vgm.C352DualChipFlag && c352Visualizer != null)
            {
                AddSecondaryVisualizer(new C352Visualizer(session.ChipRegister, c352Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.GA20, out uint ga20Clock))
            {
                ga20Visualizer = new GA20Visualizer(session.ChipRegister, ga20Clock);
                VisualizerHost.Children.Add(ga20Visualizer.Screen);
            }

            if (vgm != null && vgm.GA20DualChipFlag && ga20Visualizer != null)
            {
                AddSecondaryVisualizer(new GA20Visualizer(session.ChipRegister, ga20Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.K053260, out uint k053260Clock))
            {
                k053260Visualizer = new K053260Visualizer(session.ChipRegister, k053260Clock);
                VisualizerHost.Children.Add(k053260Visualizer.Screen);
            }

            if (vgm != null && vgm.K053260DualChipFlag && k053260Visualizer != null)
            {
                AddSecondaryVisualizer(new K053260Visualizer(session.ChipRegister, k053260Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.K054539, out uint k054539Clock))
            {
                k054539Visualizer = new K054539Visualizer(session.ChipRegister, k054539Clock);
                VisualizerHost.Children.Add(k054539Visualizer.Screen);
            }

            if (vgm != null && vgm.K054539DualChipFlag && k054539Visualizer != null)
            {
                AddSecondaryVisualizer(new K054539Visualizer(session.ChipRegister, k054539Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.RF5C164, out uint megaCdClock))
            {
                megaCdVisualizer = new MegaCDVisualizer(session.ChipRegister, megaCdClock);
                VisualizerHost.Children.Add(megaCdVisualizer.Screen);
            }

            if (vgm != null && vgm.RF5C164DualChipFlag && megaCdVisualizer != null)
            {
                AddSecondaryVisualizer(new MegaCDVisualizer(session.ChipRegister, megaCdClock, chipID: 1));
            }

            // MpcmX68k is architecturally unlike every other chip: frmMpcmX68k.cs reads
            // driver-internal state directly (see MpcmX68kVisualizer.cs's header), not via
            // ChipRegister, and there is no distinct enmInstrumentType.MpcmX68k - only
            // .mpcmpp (this port's MND/ZMS loaders always wire the mpcmpp chip model).
            if (session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.mpcmpp) && session.Driver != null)
            {
                mpcmX68kVisualizer = new MpcmX68kVisualizer(session.Driver);
                VisualizerHost.Children.Add(mpcmX68kVisualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.MultiPCM, out uint multiPcmClock))
            {
                multiPcmVisualizer = new MultiPCMVisualizer(session.ChipRegister, multiPcmClock);
                VisualizerHost.Children.Add(multiPcmVisualizer.Screen);
            }

            if (vgm != null && vgm.MultiPCMDualChipFlag && multiPcmVisualizer != null)
            {
                AddSecondaryVisualizer(new MultiPCMVisualizer(session.ChipRegister, multiPcmClock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.OKIM6258, out uint okim6258Clock))
            {
                okim6258Visualizer = new OKIM6258Visualizer(session.ChipRegister, okim6258Clock);
                VisualizerHost.Children.Add(okim6258Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.OKIM6295, out uint okim6295Clock))
            {
                okim6295Visualizer = new OKIM6295Visualizer(session.ChipRegister, okim6295Clock);
                VisualizerHost.Children.Add(okim6295Visualizer.Screen);
            }

            if (vgm != null && vgm.OKIM6295DualChipFlag && okim6295Visualizer != null)
            {
                AddSecondaryVisualizer(new OKIM6295Visualizer(session.ChipRegister, okim6295Clock, chipID: 1));
            }

            // PCM8 has no enmInstrumentType entry of its own (it's a driver-internal display,
            // not an MDSound chip instance mixed into the output) - gate on the live driver's
            // actual type instead, matching frmPCM8.cs's own Audio.DriverVirtual is MXDRV/ZMS
            // check (see PCM8Visualizer.cs's header for why the original's third RCS branch
            // is omitted).
            if (session.Driver is MDPlayer.Driver.MXDRV.MXDRV || session.Driver is MDPlayer.Driver.ZMS.ZMS)
            {
                pcm8Visualizer = new PCM8Visualizer(session.Driver);
                VisualizerHost.Children.Add(pcm8Visualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.QSound, out uint qSoundClock))
            {
                qSoundVisualizer = new QSoundVisualizer(session.ChipRegister, qSoundClock);
                VisualizerHost.Children.Add(qSoundVisualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.RF5C68, out uint rf5c68Clock))
            {
                rf5c68Visualizer = new Rf5c68Visualizer(session.ChipRegister, rf5c68Clock);
                VisualizerHost.Children.Add(rf5c68Visualizer.Screen);
            }

            if (vgm != null && vgm.RF5C68DualChipFlag && rf5c68Visualizer != null)
            {
                AddSecondaryVisualizer(new Rf5c68Visualizer(session.ChipRegister, rf5c68Clock, chipID: 1));
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.SEGAPCM, out uint segaPcmClock))
            {
                segaPcmVisualizer = new SegaPcmVisualizer(session.ChipRegister, segaPcmClock);
                VisualizerHost.Children.Add(segaPcmVisualizer.Screen);
            }

            if (session.ChipClocks.TryGetValue(MDSound.MDSound.enmInstrumentType.YMZ280B, out uint ymz280BClock))
            {
                ymz280BVisualizer = new Ymz280BVisualizer(session.ChipRegister, ymz280BClock);
                VisualizerHost.Children.Add(ymz280BVisualizer.Screen);
            }

            if (vgm != null && vgm.YMZ280BDualChipFlag && ymz280BVisualizer != null)
            {
                AddSecondaryVisualizer(new Ymz280BVisualizer(session.ChipRegister, ymz280BClock, chipID: 1));
            }

            if (sn76489Visualizer == null && ym2612Visualizer == null && ym2151Visualizer == null
                && ay8910Visualizer == null && s5bVisualizer == null && ym2413Visualizer == null
                && ym3526Visualizer == null && ym3812Visualizer == null && y8950Visualizer == null
                && ymf262Visualizer == null && ymf278bVisualizer == null && ym2203Visualizer == null
                && ym2608Visualizer == null && ym2609Visualizer == null && ym2610Visualizer == null
                && ymf271Visualizer == null && nesdmcVisualizer == null && fdsVisualizer == null
                && mmc5Visualizer == null && vrc6Visualizer == null && vrc7Visualizer == null
                && n106Visualizer == null && dmgVisualizer == null && huc6280Visualizer == null
                && k051649Visualizer == null && c140Visualizer == null && c352Visualizer == null
                && ga20Visualizer == null && k053260Visualizer == null
                && k054539Visualizer == null && megaCdVisualizer == null
                && mpcmX68kVisualizer == null && multiPcmVisualizer == null
                && okim6258Visualizer == null && okim6295Visualizer == null
                && pcm8Visualizer == null && qSoundVisualizer == null
                && rf5c68Visualizer == null && segaPcmVisualizer == null
                && ymz280BVisualizer == null) return;

            // New visualizers are constructed at 2x. Reapply the user's current toggle
            // before measuring the stack so track changes do not reset the compact view.
            ApplyChannelViewScale();
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
                huc6280Visualizer?.ScreenChangeParams();
                huc6280Visualizer?.ScreenDrawParams();
                k051649Visualizer?.ScreenChangeParams();
                k051649Visualizer?.ScreenDrawParams();
                c140Visualizer?.ScreenChangeParams();
                c140Visualizer?.ScreenDrawParams();
                c352Visualizer?.ScreenChangeParams();
                c352Visualizer?.ScreenDrawParams();
                ga20Visualizer?.ScreenChangeParams();
                ga20Visualizer?.ScreenDrawParams();
                k053260Visualizer?.ScreenChangeParams();
                k053260Visualizer?.ScreenDrawParams();
                k054539Visualizer?.ScreenChangeParams();
                k054539Visualizer?.ScreenDrawParams();
                megaCdVisualizer?.ScreenChangeParams();
                megaCdVisualizer?.ScreenDrawParams();
                mpcmX68kVisualizer?.ScreenChangeParams();
                mpcmX68kVisualizer?.ScreenDrawParams();
                multiPcmVisualizer?.ScreenChangeParams();
                multiPcmVisualizer?.ScreenDrawParams();
                okim6258Visualizer?.ScreenChangeParams();
                okim6258Visualizer?.ScreenDrawParams();
                okim6295Visualizer?.ScreenChangeParams();
                okim6295Visualizer?.ScreenDrawParams();
                pcm8Visualizer?.ScreenChangeParams();
                pcm8Visualizer?.ScreenDrawParams();
                qSoundVisualizer?.ScreenChangeParams();
                qSoundVisualizer?.ScreenDrawParams();
                rf5c68Visualizer?.ScreenChangeParams();
                rf5c68Visualizer?.ScreenDrawParams();
                segaPcmVisualizer?.ScreenChangeParams();
                segaPcmVisualizer?.ScreenDrawParams();
                ymz280BVisualizer?.ScreenChangeParams();
                ymz280BVisualizer?.ScreenDrawParams();

                // Second chip instance of any dual-chip VGM (see the vgm.XxxDualChipFlag
                // checks above) - same per-frame pull/redraw as every primary visualizer.
                foreach (IChannelVisualizer secondary in secondaryVisualizers)
                {
                    secondary.ScreenChangeParams();
                    secondary.ScreenDrawParams();
                }
            };
            visualizerTimer.Start();
        }

        private void HideVisualizers()
        {
            visualizerTimer?.Stop();
            visualizerTimer = null;
            secondaryVisualizers.Clear();
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
            huc6280Visualizer = null;
            k051649Visualizer = null;
            c140Visualizer = null;
            c352Visualizer = null;
            ga20Visualizer = null;
            k053260Visualizer = null;
            k054539Visualizer = null;
            megaCdVisualizer = null;
            mpcmX68kVisualizer = null;
            multiPcmVisualizer = null;
            okim6258Visualizer = null;
            okim6295Visualizer = null;
            pcm8Visualizer = null;
            qSoundVisualizer = null;
            rf5c68Visualizer = null;
            segaPcmVisualizer = null;
            ymz280BVisualizer = null;
            VisualizerHost.Children.Clear();
        }

        private static readonly string[] SupportedMusicExtensions =
        {
            ".vgm", ".vgz", ".xgm", ".xgz", ".sid", ".mnd", ".zms", ".zmd", ".mdx",
            ".mdr", ".nsf", ".gbs", ".hes", ".s98", ".ay", ".zgm",
        };

        private static bool IsSupportedMusicFile(IStorageFile file)
            => SupportedMusicExtensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase);

        private void OnFileDragEnter(object? sender, DragEventArgs e)
        {
            if (e.DataTransfer.Formats.Contains(DataFormat.File))
            {
                PlaylistHint.Text = "여기에 놓으면 재생 목록에 추가합니다";
            }
        }

        private void OnFileDragLeave(object? sender, DragEventArgs e)
        {
            RefreshPlaylistPanel();
        }

        private void OnFileDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private async void OnFileDrop(object? sender, DragEventArgs e)
        {
            await HandleDroppedFilesAsync(e, replacePlaylist: false);
        }

        private void OnPlaylistDragEnter(object? sender, DragEventArgs e)
        {
            OnFileDragEnter(sender, e);
            e.Handled = true;
        }

        private void OnPlaylistDragLeave(object? sender, DragEventArgs e)
        {
            OnFileDragLeave(sender, e);
            e.Handled = true;
        }

        private void OnPlaylistDragOver(object? sender, DragEventArgs e)
        {
            OnFileDragOver(sender, e);
            e.Handled = true;
        }

        private async void OnPlaylistDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            await HandleDroppedFilesAsync(e, replacePlaylist: false);
        }

        private void OnDashboardDragEnter(object? sender, DragEventArgs e)
        {
            if (e.DataTransfer.Formats.Contains(DataFormat.File))
            {
                StatusLabel.Text = "여기에 놓으면 재생 목록을 교체합니다";
            }
            e.Handled = true;
        }

        private void OnDashboardDragLeave(object? sender, DragEventArgs e)
        {
            if (!isStarting)
            {
                StatusLabel.Text = isPaused
                    ? "일시 정지"
                    : queue != null && !playbackEnded ? "재생 중" : "대기 중";
            }
            e.Handled = true;
        }

        private void OnDashboardDragOver(object? sender, DragEventArgs e)
        {
            OnFileDragOver(sender, e);
            e.Handled = true;
        }

        private async void OnDashboardDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            await HandleDroppedFilesAsync(e, replacePlaylist: true);
        }

        private async Task HandleDroppedFilesAsync(DragEventArgs e, bool replacePlaylist)
        {
            var files = e.DataTransfer.TryGetFiles()?
                .OfType<IStorageFile>()
                .Where(IsSupportedMusicFile)
                .ToArray();
            if (files == null || files.Length == 0)
            {
                PlaylistHint.Text = "지원하는 음악 파일을 드롭하세요";
                return;
            }

            System.Collections.Generic.List<PlaylistEntry> droppedEntries = await ReadPlaylistEntriesAsync(files);
            int added = droppedEntries.Count;
            if (added == 0) return;

            if (replacePlaylist)
            {
                StopPlayback();
                HideMixer();
                HideVisualizers();
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

        private async Task<int> AppendFilesAsync(System.Collections.Generic.IEnumerable<IStorageFile> files)
        {
            System.Collections.Generic.List<PlaylistEntry> entries = await ReadPlaylistEntriesAsync(files);
            playlist.AddRange(entries);
            return entries.Count;
        }

        private static async Task<System.Collections.Generic.List<PlaylistEntry>> ReadPlaylistEntriesAsync(System.Collections.Generic.IEnumerable<IStorageFile> files)
        {
            var entries = new System.Collections.Generic.List<PlaylistEntry>();
            foreach (IStorageFile file in files)
            {
                await using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                string? sourcePath = file.Path.IsFile ? file.Path.LocalPath : null;
                entries.Add(new PlaylistEntry(ms.ToArray(), file.Name, sourcePath));
            }
            return entries;
        }

        private void RefreshPlaylistPanel()
        {
            bool playlistOnly = playlistViewButton != null && playlistViewButton.IsSelected;
            SetPlaylistPanelVisibility(playlistOnly);
            PlaylistHint.Text = playlist.Count == 0
                ? "재생 목록 — 음악 파일을 여기 또는 창에 드롭하여 추가"
                : $"재생 목록 {playlist.Count}곡 — 음악 파일을 드롭하여 추가";

            synchronizingPlaylistSelection = true;
            string[] items = playlist
                .Select((entry, index) => $"{index + 1,2}. {entry.Name}")
                .ToArray();
            PlaylistList.ItemsSource = items;
            playlistViewList.ItemsSource = items;
            PlaylistList.SelectedIndex = playlistIndex;
            playlistViewList.SelectedIndex = playlistIndex;
            synchronizingPlaylistSelection = false;
            playlistViewButton.IsEnabled = playlist.Count > 0;
        }

        private async void OnPlaylistSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (synchronizingPlaylistSelection) return;
            focusedPlaylistList = PlaylistList;
            if (PlaylistList.SelectedItems.Count != 1) return;
            await SelectPlaylistIndexAsync(PlaylistList.SelectedIndex);
        }

        private async void OnPlaylistViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (synchronizingPlaylistSelection) return;
            focusedPlaylistList = playlistViewList;
            if (playlistViewList.SelectedItems.Count != 1) return;
            await SelectPlaylistIndexAsync(playlistViewList.SelectedIndex);
        }

        private void OnPlaylistGotFocus(object? sender, FocusChangedEventArgs e)
        {
            if (sender is ListBox list) focusedPlaylistList = list;
        }

        private void OnPlaylistPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is ListBox list) focusedPlaylistList = list;
        }

        // Mirrors the desktop list editor behaviour from the Windows player: ordinary
        // Cmd/Ctrl-click and Shift-click are handled by ListBox's Multiple selection mode;
        // this supplies the keyboard actions missing from the default Avalonia list.
        private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (ReferenceEquals(lastHandledPlaylistKeyEvent, e)) return;

            ListBox? list = GetPlaylistListFromKeySource(e.Source);
            list ??= focusedPlaylistList;
            if (list == null) return;
            if (!IsPlaylistEditorKey(e)) return;

            lastHandledPlaylistKeyEvent = e;
            await HandlePlaylistKeyAsync(list, e);
        }

        private static bool IsPlaylistEditorKey(KeyEventArgs e)
            => e.Key is Key.Delete or Key.Back or Key.Escape
                || (e.Key == Key.A && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0);

        private ListBox? GetPlaylistListFromKeySource(object? source)
        {
            if (source is ListBox list && IsPlaylistList(list)) return list;
            if (source is not Visual visual) return null;

            return visual.GetVisualAncestors()
                .OfType<ListBox>()
                .FirstOrDefault(IsPlaylistList);
        }

        private bool IsPlaylistList(ListBox list)
            => ReferenceEquals(list, PlaylistList) || ReferenceEquals(list, playlistViewList);

        private async Task HandlePlaylistKeyAsync(ListBox list, KeyEventArgs e)
        {
            bool selectAllModifier = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            if (e.Key == Key.A && selectAllModifier)
            {
                list.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                list.UnselectAll();
                e.Handled = true;
                return;
            }

            if (e.Key is not (Key.Delete or Key.Back)) return;

            int[] selectedIndexes = (list.SelectedItems ?? Array.Empty<object>())
                .OfType<string>()
                .Select(GetPlaylistItemIndex)
                .Where(index => index >= 0 && index < playlist.Count)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            if (selectedIndexes.Length == 0 && list.SelectedIndex is int selectedIndex
                && selectedIndex >= 0 && selectedIndex < playlist.Count)
            {
                selectedIndexes = new[] { selectedIndex };
            }
            if (selectedIndexes.Length == 0) return;

            e.Handled = true;
            await RemovePlaylistEntriesAsync(selectedIndexes);
        }

        private static int GetPlaylistItemIndex(string item)
        {
            int separator = item.IndexOf('.');
            return separator > 0 && int.TryParse(item.AsSpan(0, separator), out int oneBasedIndex)
                ? oneBasedIndex - 1
                : -1;
        }

        private async Task RemovePlaylistEntriesAsync(int[] selectedIndexes)
        {
            bool removesCurrentTrack = selectedIndexes.Contains(playlistIndex);
            bool shouldResume = removesCurrentTrack && ((queue != null && !playbackEnded) || isPaused);
            int oldPlaylistIndex = playlistIndex;

            if (removesCurrentTrack) StopPlayback();
            for (int i = selectedIndexes.Length - 1; i >= 0; i--)
            {
                playlist.RemoveAt(selectedIndexes[i]);
            }

            if (playlist.Count == 0)
            {
                playlistIndex = -1;
                loadedVgmBytes = null;
                loadedFileName = null;
                FileLabel.Text = "파일을 선택하세요";
                StatusLabel.Text = "대기 중";
                RefreshPlaylistPanel();
                UpdateTransportButtons();
                return;
            }

            int nextIndex;
            if (removesCurrentTrack)
            {
                // The item which moved into the first removed slot is the natural next song.
                nextIndex = Math.Min(selectedIndexes[0], playlist.Count - 1);
            }
            else
            {
                nextIndex = oldPlaylistIndex - selectedIndexes.Count(index => index < oldPlaylistIndex);
            }

            if (removesCurrentTrack)
            {
                await SelectPlaylistEntryAsync(nextIndex);
                if (shouldResume) await StartPlaybackAsync();
            }
            else
            {
                playlistIndex = nextIndex;
                RefreshPlaylistPanel();
                UpdateTransportButtons();
            }
        }

        private async Task SelectPlaylistIndexAsync(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= playlist.Count || selectedIndex == playlistIndex) return;

            bool shouldResume = (queue != null && !playbackEnded) || isPaused;
            StopPlayback();
            await SelectPlaylistEntryAsync(selectedIndex);
            if (shouldResume) await StartPlaybackAsync();
        }

        private async void OnPlaylistDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (playlistIndex < 0 || playlistIndex >= playlist.Count) return;
            if (queue != null && !playbackEnded) StopPlayback();
            await StartPlaybackAsync();
        }

        private async void OnPlaylistViewDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (playlistIndex < 0 || playlistIndex >= playlist.Count) return;
            if (queue != null && !playbackEnded) StopPlayback();
            await StartPlaybackAsync();
        }

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "음악 파일 열기",
                AllowMultiple = true,
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
            HideMixer();

            playlist.Clear();
            await AppendFilesAsync(files.Where(IsSupportedMusicFile));
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

        private async void OnPlayClick(object? sender, RoutedEventArgs e)
        {
            // Play is the transport's return-to-normal action. Fast/Slow are
            // temporary playback adjustments, so any Play press restores ×1
            // before it resumes, restarts, or leaves an already-playing song alone.
            bool speedReset = ResetPlaybackSpeed();
            if (isPaused && queue != null && !playbackEnded)
            {
                queue.Start();
                isPaused = false;
                StatusLabel.Text = speedReset ? "재생 계속 - 재생 속도 1x" : "재생 계속";
                UpdateTransportButtons();
                return;
            }
            if (queue != null && !playbackEnded)
            {
                if (speedReset) StatusLabel.Text = "재생 속도 1x";
                return;
            }
            await StartPlaybackAsync(showSpeedReset: speedReset);
        }

        private async Task StartPlaybackAsync(bool showSpeedReset = false)
        {
            byte[]? vgmBuf = loadedVgmBytes;
            string? fileName = loadedFileName;
            if (vgmBuf == null || isStarting) return;

            // A naturally completed AudioQueue remains allocated briefly to drain its
            // already-enqueued buffers. Dispose that stale queue before starting this track
            // again, rather than losing the reference when the new queue is assigned.
            if (queue != null)
            {
                queue.Stop();
                queue.Dispose();
                queue = null;
            }

            isStarting = true;
            playbackEnded = false;
            isPaused = false;
            UpdateTransportButtons();
            StatusLabel.Text = "로딩 중...";

            stopRequested = false;

            try
            {
                // A selected file has normally already been initialized so its channel view
                // can be displayed while waiting for Play. Keep that initialized session;
                // only fall back to loading here for callers that reach playback directly.
                MusicEngineSession? session = loadedSession;
                // A driver that naturally reached its end cannot be wound back. Reload it
                // when Play is pressed again, while leaving the last channel view on screen.
                if (session == null || session.Driver.Stopped)
                {
                    session = await Task.Run(() => MusicEngine.Load(vgmBuf, fileName));
                    loadedSession = session;
                    if (session != null)
                    {
                        ApplyChipVolumeOverrides(session);
                        fileLoopCounter = session.Driver.LoopCounter;
                    }
                }
                if (session == null || stopRequested)
                {
                    if (!stopRequested)
                        StatusLabel.Text = "오류: 이 파일은 재생할 수 없습니다 (지원하지 않는 포맷/칩, MusicEngine.cs 참고)";
                    playbackEnded = true;
                    return;
                }

                session.Driver.LoopCounter = loopEnabled ? fileLoopCounter : 0;

                await Task.Run(() =>
                {
                    CoreAudioQueue localQueue = new(
                        session.SampleRate, FramesPerBuffer, BufferCount,
                        (buf, count) =>
                        {
                            if (stopRequested || session.Driver.Stopped) return 0;
                            // Render through the session rather than calling Mds.Update()
                            // directly: SID/NSF render their own PCM, and MXDRV first
                            // renders X68000 PCM before feeding it through MDSound's mixer.
                            return session.RenderSamplesWithMasterVolume(buf, 0, count);
                        });

                    queue = localQueue;
                    localQueue.Start();
                });

                ShowVisualizersFor(session);
                ShowMixerFor(session);
                RestoreActiveView();
                StatusLabel.Text = showSpeedReset
                    ? $"재생 중 - {session.DescribeActiveChips()} - 재생 속도 1x"
                    : $"재생 중 - {session.DescribeActiveChips()}";

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
                            if (!ReferenceEquals(queue, watchedQueue)) return;
                            playbackEnded = true;
                            _ = ContinueAfterCompletionAsync();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"오류: {ex.Message}";
                playbackEnded = true;
                HideMixer();
                HideVisualizers();
            }
            finally
            {
                isStarting = false;
                UpdateTransportButtons();
            }
        }

        private void OnStopClick(object? sender, RoutedEventArgs e)
        {
            StopPlayback();
            StatusLabel.Text = "정지됨";
            // Stop is a transport action, not a view reset. Leave the selected mode intact.
            UpdateTransportButtons();
        }

        private void OnPauseClick()
        {
            if (queue == null || playbackEnded) return;

            if (isPaused)
            {
                queue.Start();
                isPaused = false;
                StatusLabel.Text = "재생 계속";
            }
            else
            {
                queue.Pause();
                isPaused = true;
                StatusLabel.Text = "일시 정지";
            }
            UpdateTransportButtons();
        }

        private async void OnPreviousClick() => await MovePlaylistAsync(-1);

        private async void OnNextClick() => await MovePlaylistAsync(1);

        private async Task MovePlaylistAsync(int delta)
        {
            if (playlist.Count == 0) return;

            bool shouldResume = (queue != null && !playbackEnded) || isPaused;
            int target = (playlistIndex + delta + playlist.Count) % playlist.Count;
            StopPlayback();
            await SelectPlaylistEntryAsync(target);
            if (shouldResume) await StartPlaybackAsync();
        }

        // Completion is treated as a transport transition, never as a view reset. For a
        // playlist, advance to the following entry; for the final entry, leave its channel
        // view visible and let Play reload the stopped driver from the beginning.
        private async Task ContinueAfterCompletionAsync()
        {
            if (playlistIndex >= 0 && playlistIndex < playlist.Count && loopEnabled)
            {
                StatusLabel.Text = "반복 재생";
                await SelectPlaylistEntryAsync(playlistIndex);
                await StartPlaybackAsync();
                return;
            }

            if (playlistIndex >= 0 && playlistIndex + 1 < playlist.Count)
            {
                StatusLabel.Text = "다음 곡 재생";
                await SelectPlaylistEntryAsync(playlistIndex + 1);
                await StartPlaybackAsync();
                return;
            }

            StatusLabel.Text = "재생 완료";
            UpdateTransportButtons();
        }

        // This deliberately describes the visualizer topology, rather than the song title
        // or chip clocks. Two tracks with the same chip instances can replace their register
        // source in-place without shrinking the already visible channel-view window.
        private static string GetChannelLayoutSignature(MusicEngineSession session)
            => string.Join(";", session.ChipVolumeSlots
                .OrderBy(slot => (int)slot.Key.Type)
                .ThenBy(slot => slot.Key.ChipId)
                .Select(slot => $"{(int)slot.Key.Type}:{slot.Key.ChipId}"));

        // Loading a file is also enough to construct its chip-register view: the player
        // starts in the channel view and remains there until Play is pressed (or auto-play
        // starts the queue). This matches the Windows player's "loaded / ready" behavior.
        private async Task SelectPlaylistEntryAsync(int index)
        {
            playlistIndex = index;
            PlaylistEntry entry = playlist[index];
            loadedVgmBytes = entry.Bytes;
            loadedFileName = entry.SourcePath ?? entry.Name;
            loadedSession = null;
            playbackEnded = true;
            isPaused = false;
            FileLabel.Text = playlist.Count > 1 ? $"{entry.Name}  ({index + 1}/{playlist.Count})" : entry.Name;
            StatusLabel.Text = "채널 뷰 준비 중...";
            RefreshPlaylistPanel();
            UpdateTransportButtons();

            MusicEngineSession? session;
            try
            {
                session = await Task.Run(() => MusicEngine.Load(entry.Bytes, entry.SourcePath ?? entry.Name));
            }
            catch (Exception ex)
            {
                // This method is ultimately reached from Avalonia async event handlers.
                // Never let a malformed/partially supported music file propagate out of one
                // of those handlers: on macOS an unhandled managed exception terminates the
                // whole app with SIGABRT rather than merely rejecting the file.
                StatusLabel.Text = $"오류: {entry.Name} 로드 실패 — {ex.Message}";
                playbackEnded = true;
                UpdateTransportButtons();
                return;
            }
            if (playlistIndex != index || !ReferenceEquals(loadedVgmBytes, entry.Bytes)) return;

            if (session == null)
            {
                StatusLabel.Text = "오류: 이 파일은 재생할 수 없습니다 (지원하지 않는 포맷/칩, MusicEngine.cs 참고)";
                return;
            }

            string newLayoutSignature = GetChannelLayoutSignature(session);
            bool keepChannelWindow = channelLayoutSignature == newLayoutSignature
                && ReferenceEquals(ViewHost.Content, VisualizerHost);
            loadedSession = session;
            ApplyChipVolumeOverrides(session);
            fileLoopCounter = session.Driver.LoopCounter;
            session.Driver.LoopCounter = loopEnabled ? fileLoopCounter : 0;
            ShowVisualizersFor(session);
            ShowMixerFor(session);
            channelLayoutSignature = newLayoutSignature;
            // Same layout: rebuild the controls against the new chip-register instance but
            // retain the current window geometry. A changed topology gets a normal fit.
            RestoreActiveView(refitChannelWindow: !keepChannelWindow);
            StatusLabel.Text = $"로드됨 - 채널 뷰 대기 ({session.DescribeActiveChips()})";
            UpdateTransportButtons();
        }

        private void ChangePlaybackSpeed(double factor)
        {
            MusicEngineSession? session = loadedSession;
            if (session == null || playbackEnded) return;

            session.Driver.vgmSpeed = Math.Clamp(session.Driver.vgmSpeed * factor, 0.25, 4.0);
            StatusLabel.Text = $"재생 속도 {session.Driver.vgmSpeed:0.##}x";
        }

        private bool ResetPlaybackSpeed()
        {
            if (loadedSession is MusicEngineSession session)
            {
                if (Math.Abs(session.Driver.vgmSpeed - 1.0) < 0.001) return false;
                session.Driver.vgmSpeed = 1.0;
                return true;
            }
            return false;
        }

        private void OnLoopClick()
        {
            loopEnabled = !loopEnabled;
            if (loadedSession is MusicEngineSession session)
            {
                session.Driver.LoopCounter = loopEnabled ? fileLoopCounter : 0;
            }
            StatusLabel.Text = loopEnabled ? "반복 재생 켜짐" : "반복 재생 꺼짐";
            UpdateTransportButtons();
        }

        private void OnVolumeViewClick(object? sender, RoutedEventArgs e) => ShowVolumeView();

        private void OnChannelViewClick(object? sender, RoutedEventArgs e) => ShowChannelView();

        private void OnResetVolumeClick(object? sender, RoutedEventArgs e)
        {
            MusicEngineSession? session = loadedSession;
            if (session == null || mixerVisualizer == null) return;

            session.ResetVolumesToDefaults();
            chipVolumeOverrides.Clear();
            masterVolumeOverride = null;
            mixerVisualizer.Refresh();
            StatusLabel.Text = "곡의 기본 볼륨으로 복원했습니다";
        }

        private void StopPlayback()
        {
            stopRequested = true;
            queue?.Stop();
            queue?.Dispose();
            queue = null;
            isPaused = false;
            playbackEnded = true;
        }
    }
}
