using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using MDPlayer;

namespace MDPlayer.UI
{
    // Audio Queue Services works with a fixed number of PCM frames, rather than a target
    // latency in milliseconds.  The values themselves intentionally mirror Windows
    // frmSetting's Output/cmbLatency list, while this mapping selects suitable queue sizes.
    // A legacy/default 300-ms setting retains the port's original 2048 x 4 configuration.
    internal readonly record struct AudioBufferConfiguration(
        int StoredLatencyMilliseconds,
        int FramesPerBuffer,
        int BufferCount,
        string DisplayName);

    internal readonly record struct OutputSettings(int LatencyMilliseconds, int WaitMilliseconds, int SampleRate);

    internal static class MacAudioLatency
    {
        private static readonly AudioBufferConfiguration[] configurations =
        {
            new(25, 512, 2, "25 ms"),
            new(50, 512, 4, "50 ms"),
            new(100, 1024, 4, "100 ms"),
            new(150, 1024, 6, "150 ms"),
            new(200, 2048, 4, "200 ms"),
            new(300, 2048, 4, "300 ms"),
            new(400, 2048, 8, "400 ms"),
            new(500, 2048, 10, "500 ms"),
        };

        public static int[] LatencyOptions { get; } = configurations
            .Select(configuration => configuration.StoredLatencyMilliseconds).ToArray();

        public static AudioBufferConfiguration Resolve(int storedLatencyMilliseconds)
        {
            return configurations
                .OrderBy(configuration => Math.Abs(configuration.StoredLatencyMilliseconds - storedLatencyMilliseconds))
                .First();
        }
    }

    internal sealed class SettingsWindow : Window
    {
        // The Windows settings dialog packs a very large number of legacy options into
        // a compact area.  Keep the parity pages dense, but make every label wrap before
        // it can be clipped by the left-hand tab strip.
        private const double SettingFontSize = 10;
        private const double GroupHeaderFontSize = 10.5;

        private static readonly int[] WaitTimeOptions = { 0, 500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000 };
        private static readonly int[] SampleRateOptions = { 4000, 8000, 16000, 44100, 48000, 96000, 192000 };

        private readonly ComboBox latencyPicker = new() { MinWidth = 120 };
        private readonly ComboBox waitTimePicker = new() { MinWidth = 120 };
        private readonly ComboBox sampleRatePicker = new() { MinWidth = 120 };
        private readonly TextBox mgsDriverPath = new() { MinWidth = 310, HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBox musicaDriverPath = new() { MinWidth = 310, HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBox musicaCompilerPath = new() { MinWidth = 310, HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBlock saveError = new()
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        public OutputSettings? SavedOutputSettings { get; private set; }

        public SettingsWindow()
        {
            Title = "MDPlayer4Mac 설정";
            // Wrapping rows make this compact width sufficient without leaving an
            // oversized empty area beside every settings group.
            Width = 850;
            Height = 650;
            MinWidth = 850;
            MinHeight = 650;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Setting setting = Setting.Load();
            latencyPicker.ItemsSource = MacAudioLatency.LatencyOptions;
            latencyPicker.SelectedItem = MacAudioLatency.Resolve(setting.outputDevice.Latency).StoredLatencyMilliseconds;
            waitTimePicker.ItemsSource = WaitTimeOptions;
            waitTimePicker.SelectedItem = ClosestOption(WaitTimeOptions, setting.outputDevice.WaitTime);
            sampleRatePicker.ItemsSource = SampleRateOptions;
            sampleRatePicker.SelectedItem = ClosestOption(SampleRateOptions, setting.outputDevice.SampleRate);
            mgsDriverPath.Text = setting.other.MgsDrvPath ?? string.Empty;
            musicaDriverPath.Text = setting.other.MusicaDriverPath ?? string.Empty;
            musicaCompilerPath.Text = setting.other.MusicaCompilerPath ?? string.Empty;

            var tabs = new TabControl
            {
                Margin = new Thickness(14, 14, 14, 8),
                TabStripPlacement = Dock.Left,
                ItemsSource = BuildSettingTabs(),
            };

            var saveButton = new Button { Content = "저장", MinWidth = 84 };
            saveButton.Click += (_, _) => Save();
            var cancelButton = new Button { Content = "취소", MinWidth = 84 };
            cancelButton.Click += (_, _) => Close(false);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { cancelButton, saveButton },
            };

            Content = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto,Auto"),
                Children =
                {
                    tabs,
                    saveError.WithGridRow(1).WithMargin(new Thickness(14, 0, 14, 4)),
                    buttons.WithGridRow(2).WithMargin(new Thickness(14, 0, 14, 14)),
                },
            };
        }

        private enum WindowsControlKind { Toggle, Choice, Field, Action, Note }

        private sealed record WindowsSettingControl(WindowsControlKind Kind, string Text);
        private sealed record WindowsSettingGroup(string Header, params WindowsSettingControl[] Controls);

        private static WindowsSettingControl Toggle(string text) => new(WindowsControlKind.Toggle, text);
        private static WindowsSettingControl Choice(string text) => new(WindowsControlKind.Choice, text);
        private static WindowsSettingControl Field(string text) => new(WindowsControlKind.Field, text);
        private static WindowsSettingControl ActionButton(string text) => new(WindowsControlKind.Action, text);
        private static WindowsSettingControl Note(string text) => new(WindowsControlKind.Note, text);

        private static readonly WindowsSettingControl[] X68kSOptions =
        {
            Choice("0 : Stereo 32kHz"), Choice("1 : Stereo 44.1kHz"), Choice("2 : Stereo 48kHz"),
            Choice("3 : Stereo 16kHz"), Choice("4 : Stereo 22.05kHz"), Choice("5 : Stereo 24kHz"),
            Choice("6 : Mono 32kHz"), Choice("7 : Mono 44.1kHz"), Choice("8 : Mono 48kHz"),
            Choice("9 : Mono 16kHz"), Choice("A : Mono 22.05kHz"), Choice("B : Mono 24kHz"), Choice("none"),
        };

        private object[] BuildSettingTabs()
        {
            return new object[]
            {
                SettingTab("Output", BuildOutputPage()),
                SettingTab("Sound", BuildSoundPage()),
                SettingTab("OPN2", BuildOpn2Page()),
                SettingTab("NSF", BuildNsfPage()),
                SettingTab("SID", BuildSidPage()),
                SettingTab("muapDotNET", BuildMuapPage()),
                SettingTab("PMDDotNET", BuildPmdPage()),
                SettingTab("X680x0", BuildX68kPage()),
                SettingTab("MIDI out", BuildMidiOutPage()),
                SettingTab("MIDI out2", BuildMidiOut2Page()),
                SettingTab("MIDIExport", BuildMidiExportPage()),
                SettingTab("MIDI Keyboard", BuildMidiKeyboardPage()),
                SettingTab("HotKeys", BuildHotKeysPage()),
                SettingTab("Mixer Balance", BuildMixerBalancePage()),
                SettingTab("PlayList", BuildPlaylistSettingsPage()),
                SettingTab("Network", BuildNetworkPage()),
                SettingTab("Other", BuildOtherPage()),
                SettingTab("Omake", BuildOmakePage()),
                SettingTab("About", BuildAboutPage()),
            };
        }

        private static TabItem SettingTab(string header, Control content) => new()
        {
            Header = new TextBlock { Text = header, FontSize = SettingFontSize },
            Content = content,
        };

        private static TextBlock SettingText(string text) => new()
        {
            Text = text,
            FontSize = SettingFontSize,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private static TextBlock SettingGroupHeader(string text) => new()
        {
            // A string Header is measured at its full one-line length by GroupBox.
            // Constraining the header itself lets long Windows section names wrap and
            // prevents them from making the whole page wider than the viewport.
            Text = text,
            FontSize = GroupHeaderFontSize,
            MaxWidth = 210,
            TextWrapping = TextWrapping.Wrap,
        };

        // This builder keeps every Windows tab's original setting names visible while
        // avoiding inactive controls that appear actionable. The Output tab is the one
        // current macOS functionality supports; every control created here is disabled
        // until its matching engine/UI feature is implemented in this port.
        private static Control BuildUnavailableSettingsPage(string note, params WindowsSettingGroup[] groups)
        {
            // Keep padding on the content, not the ScrollViewer. This places the bar
            // at the tab page's right edge instead of inside the settings groups.
            var stack = new StackPanel
            {
                Margin = new Thickness(10, 10, 12, 10),
                Spacing = 8,
            };
            stack.Children.Add(new TextBlock
            {
                Text = note,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                FontSize = SettingFontSize,
            });

            foreach (WindowsSettingGroup group in groups)
            {
                var controls = new StackPanel { Margin = new Thickness(8, 5), Spacing = 2 };
                foreach (WindowsSettingControl control in group.Controls)
                    controls.Children.Add(BuildUnavailableControl(control));
                stack.Children.Add(new GroupBox
                {
                    Header = SettingGroupHeader(group.Header),
                    FontSize = GroupHeaderFontSize,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = controls,
                });
            }

            var scrollViewer = new ScrollViewer { Content = stack };
            ScrollViewer.SetVerticalScrollBarVisibility(scrollViewer, ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(scrollViewer, ScrollBarVisibility.Disabled);
            return scrollViewer;
        }

        private static Control BuildUnavailableControl(WindowsSettingControl control)
        {
            return control.Kind switch
            {
                WindowsControlKind.Toggle => new CheckBox { Content = SettingText(control.Text), IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch },
                WindowsControlKind.Choice => new RadioButton { Content = SettingText(control.Text), IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch },
                WindowsControlKind.Field => new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("205,*"),
                    ColumnSpacing = 7,
                    Children =
                    {
                        SettingText(control.Text),
                        new TextBox { MinWidth = 150, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch }.WithGridColumn(1),
                    },
                },
                WindowsControlKind.Action => new Button { Content = SettingText(control.Text), FontSize = SettingFontSize, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left },
                _ => new TextBlock { Text = control.Text, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, FontSize = SettingFontSize },
            };
        }

        private static Control BuildOpn2Page() => BuildUnavailableSettingsPage(
            "Windows OPN2 emulator-detail options. The current macOS engine uses its configured default core.",
            new("Nuked-OPN2 Emulation type",
                Choice("YM2612(without filter emulation)"), Choice("YM2612(MD1,MD2 VA2)(default)"),
                Choice("Discrete(Teradrive)"), Choice("ASIC(with lowpass filter)"), Choice("ASIC(MD1 VA7,MD2,MD3,etc)")),
            new("Gens Emulation option", Toggle("SSG-EG Enable"), Toggle("DAC Highpass Enable")));

        private static Control BuildNsfPage() => BuildUnavailableSettingsPage(
            "Windows NSF-chip emulation options. NSF playback is available, but these detailed model controls are not yet wired to the macOS engine.",
            new("NES", Toggle("Non-linear mixer"), Toggle("Phase refresh"), Toggle("Duty swap"), Toggle("Unmute on reset")),
            new("FDS", Field("LPF (Hz)"), Toggle("Write disable($8000 - $DFFF)"), Toggle("$4085 reset")),
            new("MMC5", Toggle("Non-linear mixer"), Toggle("Phase refresh")),
            new("N160", Toggle("Serial")),
            new("DMC", Toggle("Unmute on reset"), Toggle("Non-linear mixer"), Toggle("Enable $4011"), Toggle("Enable PNoise"),
                Toggle("DPCM anti-click"), Toggle("Randomize noise"), Toggle("Triangle mute"), Toggle("Randomize triangle"), Toggle("DPCM reverse")),
            new("Filters", Field("HPF"), Field("LPF")));

        private static Control BuildSidPage() => BuildUnavailableSettingsPage(
            "Windows SID configuration. The macOS port uses its existing libsidplayfp playback path; per-model options are not yet exposed to it.",
            new("SID model", Toggle("Force"), Choice("MOS 6581"), Choice("MOS 8580")),
            new("C64 Model", Toggle("Force"), Choice("PAL"), Choice("NTSC"), Choice("Old NTSC"), Choice("DREAN")),
            new("Quality", Choice("1 - Low (Light)"), Choice("2"), Choice("3 - Middle"), Choice("4 - High (Heavy)")),
            new("ROM Image", Field("Kernal"), ActionButton("..."), Field("Basic"), ActionButton("..."), Field("Character"), ActionButton("...")),
            new("Output", Field("OutputBuffer size")));

        private static Control BuildMuapPage() => BuildUnavailableSettingsPage(
            "muapDotNET sound-device selection is Windows-driver specific and is unavailable on macOS.",
            new WindowsSettingGroup("Sound Device Mode", Choice("98CanBe(86B+WSS)+OPN2 (Default)"), Choice("OTOMI Chan(OPNA+ADPCM+OPN2)")));

        private static Control BuildPmdPage() => BuildUnavailableSettingsPage(
            "PMDDotNET advanced compiler, board, PPSDRV and manual-volume controls are retained as Windows-compatible UI only.",
            new("Mode", Choice("Auto"), Choice("Manual"), Field("Compiler arguments"), ActionButton("reset"), Field("Driver arguments"), ActionButton("clear")),
            new("Manual", Toggle("Set volume(manual)"), Toggle("Use PPZ8"), Toggle("Use PPSDRV")),
            new("Select board", Choice("Normal board"), Choice("Speak board"), Choice("86 board")),
            new("PPSDRV", Choice("Use interface default"), Choice("Manual frequency"), Field("(Real Chip) Rendering Frequency (Hz)"), Field("(SCCI) Send synchronize wait value")),
            new("Manual volume", Field("FM"), Field("SSG"), Field("Rhythm"), Field("ADPCM"), Field("(GIMIC) SSG volume")));

        private static Control BuildX68kPage()
        {
            var tab = new TabControl
            {
                ItemsSource = new object[]
                {
                    new TabItem { Header = "Zmusic", Content = BuildUnavailableSettingsPage("Zmusic driver settings are not yet configurable on macOS.",
                        new("MPCM type", Choice("MPCM"), Choice("MPCMPP (mercury unit)")),
                        new("PCM8 type", Choice("PCM8 (use X68Sound)"), Choice("PCM8PP (mercury unit)")),
                        new("S option", X68kSOptions),
                        new("Support File", Field("Wait time to next play (ms)")),
                        new("Compile priority", Choice("V3 -> V2 (default)"), Choice("V2 -> V3"), Choice("V2 only"), Choice("V3 only"))) },
                    new TabItem { Header = "MXDRV", Content = BuildUnavailableSettingsPage("MXDRV driver settings are not yet configurable on macOS.",
                        new("PCM8 type", Choice("PCM8 (use X68Sound)"), Choice("PCM8PP (mercury unit)")),
                        new("S option", X68kSOptions)) },
                    new TabItem { Header = "MNDRV", Content = BuildUnavailableSettingsPage("MNDRV driver settings are not yet configurable on macOS.",
                        new WindowsSettingGroup("MPCM type", Choice("MPCM"), Choice("MPCMPP (mercury unit)"))) },
                    new TabItem { Header = "RCS", Content = BuildUnavailableSettingsPage("RCS driver settings are not yet configurable on macOS.",
                        new WindowsSettingGroup("PCM8 type", Choice("PCM8 (use X68Sound)"), Choice("PCM8PP (mercury unit)"))) },
                },
            };
            return tab;
        }

        private static Control BuildMidiOutPage() => BuildUnavailableSettingsPage(
            "MID/RCP/RCS 재생은 macOS 내장 DLS General MIDI 신시사이저로 출력함. 외부 MIDI 장치와 VST 라우팅은 아직 지원하지 않음.",
            new("MIDI Out List", ActionButton("↓ +"), ActionButton("-"), ActionButton("Add VST"), Note("Lists A through J")),
            new("MIDI Out Device Palette", Note("Device palette and ordering controls")));

        private static Control BuildMidiOut2Page() => BuildUnavailableSettingsPage(
            "Windows pre-send MIDI reset strings are not available without a macOS MIDI output backend.",
            new WindowsSettingGroup("Before Send", Field("GM SystemOn"), Field("GS Reset"), Field("XG SystemOn"), Field("Custom"), Field("Format: (delayTime(dec)):(command data(hex)),...;..."), ActionButton("Default")));

        private static Control BuildMidiExportPage() => BuildUnavailableSettingsPage(
            "MIDI file export is not implemented in the macOS player yet.",
            new("MIDI Export", Toggle("Export MIDI File while playing"), Toggle("Output without Audio"), Toggle("Evaluate fnum only on KeyON"), Toggle("Add Control for VOPMex"), Field("Output path"), ActionButton("...")),
            new("Target IC", Toggle("YM2612"), Toggle("SN76489"), Toggle("YM2151"), Toggle("YM2203"), Toggle("YM2608"), Toggle("YM2610B"), Toggle("Primary / Secondary")));

        private static Control BuildMidiKeyboardPage() => BuildUnavailableSettingsPage(
            "MIDI keyboard input is not implemented in the macOS player yet.",
            new("MIDI Keyboard", Toggle("Use MIDI Keyboard"), Field("MIDI IN"), Choice("MONO"), Choice("POLY")),
            new("use channel", Toggle("FM1"), Toggle("FM2"), Toggle("FM3"), Toggle("FM4"), Toggle("FM5"), Toggle("FM6")),
            new("Control by CC(Control Change) Data", Field("Play"), Field("Stop"), Field("Fast"), Field("Slow"), Field("Pause"), Field("Fadeout"), Field("Previous"), Field("Next")));

        private static Control BuildHotKeysPage() => BuildUnavailableSettingsPage(
            "Windows global keyboard-hook features are not implemented on macOS. The player retains its local dashboard controls.",
            new("Keyboard hook", Toggle("Use keyboard hook"), Toggle("Play"), Toggle("Stop"), Toggle("Pause"), Toggle("Fadeout"), Toggle("Previous"), Toggle("Next"), Toggle("Slow"), Toggle("Fast")),
            new("Shortcut assignment", Field("Speed reset"), Field("Speed up"), Field("Speed down"), Field("Play P.List Cur."), Field("Down P.List Cur."), Field("Up P.List Cur."), Field("Reset M.Vol"), Field("Down M.Vol"), Field("Up M.Vol"), Field("Fwd")));

        private static Control BuildMixerBalancePage() => BuildUnavailableSettingsPage(
            "The macOS player already persists mixer gains from its Volume view. Windows automatic balance calculation is not implemented yet.",
            new("Auto Balance", Toggle("Use Auto Balance"), Choice("Same position as song data"), Choice("Not same position as song data")),
            new("Save song balance", Choice("Save song balance"), Choice("Do not save song balance")),
            new("Mixer Balance", Note("Per-chip balance is available in the Volume view.")));

        private static Control BuildPlaylistSettingsPage() => BuildUnavailableSettingsPage(
            "The macOS player supports its embedded playlist; these Windows automatic companion-file actions are not implemented yet.",
            new("PlayList", Toggle("Empty PlayList")),
            new("Auto open", Toggle("Auto open text"), Field("Text extensions"), Toggle("Auto open MML"), Field("MML extensions"), Toggle("Auto open image"), Field("Image extensions")));

        private static Control BuildNetworkPage() => BuildUnavailableSettingsPage(
            "MDServer network playback is not implemented in this macOS port.",
            new WindowsSettingGroup("MDServer", Toggle("Use MDServer"), Field("Port")));

        private Control BuildOtherPage()
        {
            var chooseMgsDriver = new Button { Content = "Choose…", MinWidth = 84 };
            chooseMgsDriver.Click += async (_, _) => await ChooseMgsDriverAsync();
            var mgsGroup = new GroupBox
            {
                Header = SettingGroupHeader("MGSDRV (.mgs playback)"),
                Content = new StackPanel
                {
                    Margin = new Thickness(8, 5), Spacing = 5,
                    Children =
                    {
                        SettingText("Choose your own authorized MGSDRV.COM. MDPlayer4Mac does not include or redistribute this driver."),
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { mgsDriverPath, chooseMgsDriver } },
                    },
                },
            };
            var chooseMusicaDriver = new Button { Content = "Choose…", MinWidth = 84 };
            chooseMusicaDriver.Click += async (_, _) => await ChooseMusicaFileAsync(musicaDriverPath, "KINROU5.DRV 선택", "KINROU5.DRV", "*.DRV");
            var chooseMusicaCompiler = new Button { Content = "Choose…", MinWidth = 84 };
            chooseMusicaCompiler.Click += async (_, _) => await ChooseMusicaFileAsync(musicaCompilerPath, "KINROU4.COM 선택", "KINROU4.COM", "*.COM");
            var musicaGroup = new GroupBox
            {
                Header = SettingGroupHeader("MuSICA (.msd / .bgm playback)"),
                Content = new StackPanel
                {
                    Margin = new Thickness(8, 5), Spacing = 5,
                    Children =
                    {
                        SettingText("Choose your own authorized MuSICA programs. .bgm uses KINROU5.DRV; compiling .msd also needs KINROU4.COM. Neither file is bundled or redistributed."),
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { musicaDriverPath, chooseMusicaDriver } },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { musicaCompilerPath, chooseMusicaCompiler } },
                    },
                },
            };
            var unavailable = BuildUnavailableSettingsPage(
                "These Windows-wide preferences are listed for parity. The currently implemented macOS controls remain available directly in the player UI.",
                new("Playback", Toggle("Use loop times"), Field("Loop times"), Toggle("Initialize always"), Toggle("Auto open"), Toggle("Non-rendering for pause")),
                new("Screen", Field("Screen frame rate"), Toggle("ExALL"), Toggle("Toast"), Toggle("Tappy mode")),
                new("WAV", Toggle("Output WAV file"), Field("WAV output path"), ActionButton("...")),
                new("Register dump", Toggle("Dump switch"), Field("Dump output path"), ActionButton("...")),
                new("Paths", Field("Data path"), ActionButton("..."), Field("Search path"), ActionButton("..."), Field("Image resource file"), ActionButton("...")),
                new("Other", Toggle("Use GetInst"), Toggle("Save compiled file"), ActionButton("Reset window positions"), ActionButton("Open setting folder")));
            return new StackPanel { Margin = new Thickness(10), Spacing = 8, Children = { mgsGroup, musicaGroup, unavailable } };
        }

        private async Task ChooseMgsDriverAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "MGSDRV.COM 선택",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("MGSDRV.COM") { Patterns = new[] { "MGSDRV.COM", "*.COM" } } },
            });
            if (files.Count > 0 && files[0].Path.IsFile) mgsDriverPath.Text = files[0].Path.LocalPath;
        }

        private async Task ChooseMusicaFileAsync(TextBox target, string title, string expectedName, string pattern)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType(expectedName) { Patterns = new[] { expectedName, pattern } } },
            });
            if (files.Count > 0 && files[0].Path.IsFile) target.Text = files[0].Path.LocalPath;
        }

        private static Control BuildOmakePage() => BuildUnavailableSettingsPage(
            "Windows diagnostic and VST options are not implemented in the macOS port.",
            new("VST", Field("VST path"), ActionButton("..."), Field("SCC base address")),
            new("Debug", Toggle("Show console"), Toggle("Display frame counter")),
            new("Log level", Choice("Trace"), Choice("Debug"), Choice("Error"), Choice("Warning"), Choice("Information")));

        private Control BuildOutputPage()
        {
            var devicePicker = new ComboBox
            {
                ItemsSource = new[] { "System Default Output" },
                SelectedIndex = 0,
                IsEnabled = false,
                MinWidth = 290,
            };

            var outputDeviceGroup = new GroupBox
            {
                Header = "Output Devices",
                FontSize = GroupHeaderFontSize,
                Content = new StackPanel
                {
                    Margin = new Thickness(8, 5),
                    Spacing = 4,
                    Children =
                    {
                        new RadioButton { Content = SettingText("Core Audio"), IsChecked = true, IsEnabled = false },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 7,
                            Children = { SettingText("Device").WithWidth(58), devicePicker },
                        },
                        new TextBlock
                        {
                            Text = "macOS에서는 시스템 기본 출력 장치를 사용합니다. 장치 변경은 제어 센터 또는 시스템 설정에서 합니다.",
                            Foreground = Brushes.Gray,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = SettingFontSize,
                        },
                    },
                },
            };

            var timingGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                ColumnSpacing = 7,
                RowSpacing = 6,
                Children =
                {
                    SettingText("Latency (Rendering Buffer)").WithGridRow(0),
                    latencyPicker.WithGridRow(0).WithGridColumn(1),
                    SettingText("ms").WithGridRow(0).WithGridColumn(2),
                    SettingText("Wait time before playing").WithGridRow(1),
                    waitTimePicker.WithGridRow(1).WithGridColumn(1),
                    SettingText("ms").WithGridRow(1).WithGridColumn(2),
                    SettingText("Sample Rate").WithGridRow(2),
                    sampleRatePicker.WithGridRow(2).WithGridColumn(1),
                    SettingText("Hz").WithGridRow(2).WithGridColumn(2),
                },
            };

            return new StackPanel
            {
                Margin = new Thickness(10),
                Spacing = 8,
                Children =
                {
                    outputDeviceGroup,
                    timingGrid,
                    new TextBlock
                    {
                        Text = "출력 설정은 다음 재생부터 적용됩니다.",
                        Foreground = Brushes.Gray,
                        FontSize = SettingFontSize,
                    },
                },
            };
        }

        // This is the complete group list from Windows ucSettingInstruments.  The macOS
        // engine is deliberately simpler today: it always wires the first software emulator
        // shown in each group.  All alternatives remain visible but disabled until their
        // engine paths exist here too; in particular, there is no C86ctl/SCCI/GIMIC support.
        private sealed record SoundChipSource(string Name, params string[] EmulatorChoices);

        private static readonly SoundChipSource[] SoundChipSources =
        {
            new("YM2151(OPM)(Primary)", "Emu (fmgen)", "Emu (mame)", "Emu (X68Sound)"),
            new("YM2151(OPM)(Secondary)", "Emu (fmgen)", "Emu (mame)", "Emu (X68Sound)"),
            new("YM2203(OPN)(Primary)", "Emulation"),
            new("YM2203(OPN)(Secondary)", "Emulation"),
            new("YM2608(OPNA)(Primary)", "Emulation"),
            new("YM2608(OPNA)(Secondary)", "Emulation"),
            new("YM2610/B(OPNB)(Primary)", "Emulation"),
            new("YM2610/B(OPNB)(Secondary)", "Emulation"),
            new("YM2612(OPN2)(Primary)", "Emulation (Gens)", "Emulation (mame)", "Emulation (Nuked)"),
            new("YM2612(OPN2)(Secondary)", "Emulation (Gens)", "Emulation (mame)", "Emulation (Nuked)"),
            new("SN76489(DCSG)(Primary)", "Emulation (maxim)", "Emulation (mame)"),
            new("SN76489(DCSG)(Secondary)", "Emulation (maxim)", "Emulation (mame)"),
            new("YM2612(OPN2)(Use SCCI module Only!)"),
            new("C140(Secondary)", "Emulation"),
            new("C140(Primary)", "Emulation"),
            new("SEGAPCM(Secondary)", "Emulation"),
            new("SEGAPCM(Primary)", "Emulation"),
            new("YMF262(OPL3)(Primary)", "Emulation"),
            new("YMF262(OPL3)(Secondary)", "Emulation"),
            new("YM3812(OPL2)(Primary)", "Emulation"),
            new("YM3812(OPL2)(Secondary)", "Emulation"),
            new("YM3526(OPL)(Secondary)", "Emulation"),
            new("YM3526(OPL)(Primary)", "Emulation"),
            new("YM2413(Secondary)", "Emulation"),
            new("YM2413(Primary)", "Emulation"),
            new("AY-3-8910(Primary)", "Emulation (fmgen)", "Emulation (mame)"),
            new("AY-3-8910(Secondary)", "Emulation (fmgen)", "Emulation (mame)"),
            new("K051649 / K052539 SCC+(Secondary)", "Emulation"),
            new("K051649 / K052539 SCC+(Primary)", "Emulation"),
        };

        private static Control BuildSoundPage()
        {
            var chipSelection = new GroupBox
            {
                Header = "Sound Chip Selection",
                FontSize = GroupHeaderFontSize,
                Content = new StackPanel
                {
                    Margin = new Thickness(8, 5),
                    Spacing = 2,
                    Children =
                    {
                        new CheckBox
                        {
                            Content = SettingText("Do not use C86ctl/SCCI (reboot is required)"),
                            IsChecked = true,
                            IsEnabled = false,
                        },
                        new TextBlock
                        {
                            Text = "macOS 포트는 현재 C86ctl/SCCI/GIMIC 실칩 출력을 지원하지 않으므로 모든 칩을 소프트웨어 에뮬레이터로 재생합니다.",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.Gray,
                            FontSize = SettingFontSize,
                        },
                    },
                },
            };

            var delayedPlayback = new GroupBox
            {
                Header = "Delayed Playback",
                FontSize = GroupHeaderFontSize,
                Content = new StackPanel
                {
                    Margin = new Thickness(8, 5),
                    Spacing = 3,
                    Children =
                    {
                        new CheckBox
                        {
                            Content = SettingText("Opportunistic mode (Recommended if rendering buffer is below 100ms)"),
                            IsEnabled = false,
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            Children =
                            {
                                SettingText("Emulation").WithWidth(62),
                                new TextBox { Text = "0", Width = 70, IsEnabled = false },
                                SettingText("ms"),
                                SettingText("Real").WithWidth(29).WithMargin(new Thickness(9, 0, 0, 0)),
                                new TextBox { Text = "0", Width = 70, IsEnabled = false },
                                SettingText("ms"),
                            },
                        },
                    },
                },
            };

            var chipGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*"),
                RowSpacing = 5,
            };
            for (int index = 0; index < SoundChipSources.Length; index++)
            {
                chipGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Control card = BuildChipSourceCard(SoundChipSources[index], index);
                Grid.SetRow(card, index);
                chipGrid.Children.Add(card);
            }

            var scrollViewer = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(10, 10, 12, 10),
                    Spacing = 8,
                    Children = { chipSelection, delayedPlayback, chipGrid },
                },
            };
            ScrollViewer.SetVerticalScrollBarVisibility(scrollViewer, ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(scrollViewer, ScrollBarVisibility.Disabled);
            return scrollViewer;
        }

        private static Control BuildChipSourceCard(SoundChipSource source, int index)
        {
            string groupName = $"sound-source-{index}";
            var options = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            if (source.EmulatorChoices.Length == 0)
            {
                options.Children.Add(ChipOption(new CheckBox { Content = SettingText("Send wait"), IsEnabled = false }));
                options.Children.Add(ChipOption(new CheckBox { Content = SettingText("Twice"), IsEnabled = false }));
                options.Children.Add(ChipOption(new CheckBox { Content = SettingText("Emulation PCM Only"), IsEnabled = false }));
            }
            else
            {
                for (int emulatorIndex = 0; emulatorIndex < source.EmulatorChoices.Length; emulatorIndex++)
                {
                    options.Children.Add(ChipOption(new RadioButton
                    {
                        Content = SettingText(source.EmulatorChoices[emulatorIndex]),
                        GroupName = groupName,
                        IsChecked = emulatorIndex == 0,
                        IsHitTestVisible = false,
                        IsEnabled = emulatorIndex == 0,
                    }));
                }
                options.Children.Add(ChipOption(new RadioButton { Content = SettingText("Silent"), GroupName = groupName, IsEnabled = false }));
                options.Children.Add(ChipOption(new RadioButton { Content = SettingText("Real"), GroupName = groupName, IsEnabled = false }));
                options.Children.Add(ChipOption(new ComboBox
                {
                    ItemsSource = new[] { "No compatible real chip" },
                    SelectedIndex = 0,
                    Width = 150,
                    IsEnabled = false,
                }));
            }

            return new GroupBox
            {
                Header = SettingGroupHeader(source.Name),
                FontSize = GroupHeaderFontSize,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new StackPanel
                {
                    Margin = new Thickness(7, 4),
                    Spacing = 2,
                    Children =
                    {
                        options,
                        new TextBlock
                        {
                            Text = source.EmulatorChoices.Length == 0
                                ? "SCCI module options are unavailable on macOS"
                                : "Real/SCCI device output is unavailable on macOS",
                            Foreground = Brushes.Gray,
                            FontSize = SettingFontSize,
                        },
                    },
                },
            };
        }

        private static Control ChipOption(Control control)
        {
            control.Margin = new Thickness(0, 0, 10, 3);
            return control;
        }

        private Control BuildAboutPage()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.4";
            var artwork = new Image
            {
                Width = 115,
                Height = 279,
                Stretch = Stretch.Fill,
                VerticalAlignment = VerticalAlignment.Top,
            };
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://MDPlayer4Mac/Assets/About/FeliAndMD2.png"));
                artwork.Source = new Bitmap(stream);
            }
            catch
            {
                artwork.IsVisible = false;
            }

            var description = new TextBox
            {
                Text = "# MDPlayer4Mac\nVGM, VGZ, MDX를 비롯한 레트로 음악 파일을 칩 에뮬레이션으로 재생하는 macOS 포트입니다.",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 93,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var githubButton = new Button
            {
                Content = "Open latest version page of Github.",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            githubButton.Click += async (_, _) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Launcher != null)
                    await topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/ToughkidDev/MDPlayer4Mac"));
            };

            var details = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"),
                RowSpacing = 7,
                Children =
                {
                    new TextBlock { Text = "MDPlayer4Mac", FontSize = 19, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = $"Version {version}", FontFamily = new FontFamily("Menlo") }.WithGridRow(1),
                    new TextBlock { Text = "Copyright © ToughkidDev", Foreground = Brushes.Gray }.WithGridRow(2),
                    new TextBlock { Text = "Based on MDPlayer by kuma4649", Foreground = Brushes.Gray }.WithGridRow(3),
                    description.WithGridRow(4),
                    githubButton.WithGridRow(5),
                },
            };

            var layout = new Grid
            {
                Margin = new Thickness(18),
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 17,
                Children = { artwork, details.WithGridColumn(1) },
            };
            Grid.SetRowSpan(artwork, 6);
            return layout;
        }

        private void Save()
        {
            if (latencyPicker.SelectedItem is not int latency
                || waitTimePicker.SelectedItem is not int waitTime
                || sampleRatePicker.SelectedItem is not int sampleRate) return;

            try
            {
                Setting setting = Setting.Load();
                setting.outputDevice.Latency = latency;
                setting.outputDevice.WaitTime = waitTime;
                setting.outputDevice.SampleRate = sampleRate;
                setting.other.MgsDrvPath = mgsDriverPath.Text?.Trim() ?? string.Empty;
                setting.other.MusicaDriverPath = musicaDriverPath.Text?.Trim() ?? string.Empty;
                setting.other.MusicaCompilerPath = musicaCompilerPath.Text?.Trim() ?? string.Empty;
                setting.Save();
                SavedOutputSettings = new OutputSettings(latency, waitTime, sampleRate);
                Close(true);
            }
            catch (Exception ex)
            {
                saveError.Text = $"설정을 저장하지 못했습니다: {ex.Message}";
                saveError.IsVisible = true;
            }
        }

        private static int ClosestOption(int[] options, int value)
            => options.OrderBy(option => Math.Abs(option - value)).First();
    }

    internal static class SettingsWindowLayoutExtensions
    {
        // Code-created layouts remain much more legible with these two tiny helpers than
        // with repeated Grid.SetRow / Margin assignments after each collection initializer.
        public static T WithGridRow<T>(this T control, int row) where T : Control
        {
            Grid.SetRow(control, row);
            return control;
        }

        public static T WithMargin<T>(this T control, Thickness margin) where T : Control
        {
            control.Margin = margin;
            return control;
        }

        public static T WithWidth<T>(this T control, double width) where T : Control
        {
            control.Width = width;
            return control;
        }

        public static T WithGridColumn<T>(this T control, int column) where T : Control
        {
            Grid.SetColumn(control, column);
            return control;
        }
    }
}
