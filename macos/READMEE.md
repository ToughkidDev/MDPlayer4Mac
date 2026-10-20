[한국어](README.md) | **English** | [日本語](READMEJ.md)

# MDPlayer4Mac

A personal porting project to make the Windows MDPlayer usable on macOS. The original
WinForms code is kept as-is in the repository's `MDPlayer/`, and the macOS code is maintained
separately as `.NET 8` projects under `macos/`.

Last updated: 2026-10-20

## In development

- Added compiled MUCOM88 `.mub` and source `.muc` files to the playback path. `.muc` files are
  compiled to MUB in memory before playback, with fixes so the MUCOM compiler correctly
  interprets macOS/Git LF line endings and indented extended track declarations. YM2608
  (FM, SSG, rhythm, ADPCM) for regular MUB, and the 2×YM2608, 2×YM2610 and YM2151
  configurations of extended `muPb` MUB, are initialized only for the parts actually present in
  the file. The volume view and channel view use the existing chip register path unchanged.
- Linked the official `kuma4649/mucomDotNET` source as a Git submodule. GitHub Release builds
  also fetch the submodule and include the driver DLL in the app bundle.
- Additionally linked the official `kuma4649` driver sources used by the Windows version as
  submodules. Added PMD's `.mml`/`.m`/`.m2`/`.mz`, MoonDriver's `.mdl`/`.mdr`, and MUAP98's
  `.mus`/`.o`/`.ox`/`.oy` to the playback path. Each uses the same configuration as the
  original: YM2608+PPZ8/PPSDRV/P86, YMF278B (or YMF262), and YM2608+YM2612+CS4231
  respectively. Source formats are compiled in memory without temporary files, and PMD's
  PPC/PPS/PPZ/P86 and MoonDriver's same-name `.pcm` auxiliary data are looked up in the same
  folder as the song file.
- Added playback of MuSICA compiled data `.bgm`, source `.msd`, and NRTDRV `.nrd`. Because MuSICA
  runs the original MSX driver, `.bgm` requires a user-specified `KINROU5.DRV`, and compiling
  `.msd` on the fly also requires `KINROU4.COM`. Neither program is bundled with the app.
  `.m3u` files open as playlists, expanding the supported music files at relative paths into
  the current playlist.

## v0.2.3

- Supports dedicated five-channel SCC/SCC+ note scores in `msPb0102`: `SCCA`–`SCCE`, inline `@W` waves and SCWD banks, including the independent fifth wave and linear volume.
- Keeps SCC playback separate from OPNA FM/rhythm/ADPCM and native MSX PSG. Existing msPb0100/0101 playback remains supported.
- Fixes MUC repeat jump corruption when merging a following tied note, and false music-size overflow caused by unused pages.
- Adds SCC5, mixed OPNA/PSG, waveform-bank, repeat/loop and invalid-input regression coverage.
  See the [release notes](release-notes/v0.2.3.md) and [MSX playback](docs/MSX-PLAYBACK.md).

## v0.2.2

- Excluded msPb's tempo-setting-only OPNA part from the audio mixer, fixing a mismatch in
  playback volume between MUC and MUB for the same PSG/SCC performance. Actual OPNA
  performance and the SCC-SSG fallback are retained.
- Improved chip detection for loop entry points and incomplete commands, and made silent songs
  containing only tempo and rests play.
- Fixed OPNA rhythm initialization and the auto-pan wait command for multiple drums.
- Applied the latest MUCOMSX default tempo, SCC waveform transfer and caching, raw command
  parsing, and MUC BOM/EOF handling.
- Passed playback and termination checks for 20 MUB and 20 MUC files, plus chip detection and
  reload regression checks. See the [changes](release-notes/v0.2.2.md) and
  [MSX playback](docs/MSX-PLAYBACK.md).

## v0.2.1

- Supports MUCOMSX raw v2 PSG, SCC/SCC+, and source OPNA SSG register transfers.
  Initializes emulators to match the declared chips and handles the independent waveform of
  SCC+ channel 5.
- Corrected the MSX PSG emulator clock and SSG clock conversion.
- Raw streams use the same integer-sample timer as the MSX player, reducing accumulated
  playback time error during tempo changes and timer responses. The regular MUCOM timer is
  retained.
- Added regression checks including 15 synthetic MUB and 15 synthetic MUC files and Z80
  register/timer comparisons. For details, see [MSX playback](docs/MSX-PLAYBACK.md).

## v0.2.0

- Supports MUCOMSX `msPb0100`/`msPb0101` MUB and MUC with native `MSX` parts.
  Handles MSX-MUSIC, internal PSG, and SCC+ SSG playback, plus extended waveform and gain
  commands.
- Maintains modified versions of the MUCOM compiler and driver as a separate project, including
  OPNA FM3 mode preservation, a K part register routing fix, and a fix making `#PCMInvert`
  apply only to ADPCM.
- Bundles OPNA rhythm WAVs with the app, and adds MSX playback regression tests and register
  comparison data for the Z80 driver.
- Includes FMP, NDP, and WAV/AIFF/MP3/M4A/AAC/FLAC/OGG playback added since the previous
  release, as well as external driver path settings and Xcode build support.

## v0.1.7

- Fixed the MSX-AUDIO (Y8950) ADPCM channel view showing only the fixed Total Level from the
  register. It now uses the actual decoded Delta-T ADPCM signal, so the gauge moves during
  playback and is shown relative to the original signal regardless of the user gain in the
  volume view.

## v0.1.6

- Reads the K051649/K052539 flag in VGM to display regular SCC as `SCC` and SCC+ as `SCC+`.
  Independent wave RAM writes for SCC+ channel 5 are also handled as-is.
- Long strings such as title, composer, and Notes in the song information screen are no longer
  truncated with `...`. Widening the window also widens the information column so the full
  content can be read.
- When dropping files onto the dashboard to replace the playlist, the window size is kept if
  the song has the same chip configuration. The size is readjusted only when the actual channel
  panel layout changes.

## Earlier porting work

### v0.1.5

- Added an Avalonia settings window based on the Windows original's `frmSetting` layout. The
  original tabs can be viewed, including `Output`, `Sound`, per-chip emulation, MIDI, playlist,
  network, other, and About. Currently Output is the only part actually wired up on macOS;
  Windows-only features are shown disabled so they do not misbehave.
- Output configures the Core Audio system default device, rendering buffer latency
  (25–500 ms), pre-playback wait time, and sample rate. Saved output settings are applied with a
  new Audio Queue on the next playback.
- The settings window wraps long section titles and options, and section boxes are fixed so
  they do not exceed the tab content width. The vertical scrollbar is placed at the right edge
  of the tab.
- Imported the Setting sprite and About illustration from the Windows original. The settings
  icon is split out as a 16×16 utility button at the bottom right of the dashboard so it does
  not take up timeline width.
- Added dashboard keyboard shortcuts. `Q W E R T Y U` map to Stop/Pause/Previous/Slow/
  Play/Fast/Next, and `A S D F G H J` to Open/Playlist/Information/Volume/Channel/Zoom/
  Loop, in that order. Disabled buttons cannot be triggered from the keyboard either, and
  playlist editing keys such as Cmd/Ctrl+A, Delete, and Backspace are retained.

### v0.1.4

- Fully reviewed the level meter paths in the channel view, including PCM and ADPCM families.
  Meter defaults before playback and after Stop are reset to 0, and ADPCM-A/ADPCM-B meters on
  YM2610/YM2608 and others were corrected to change according to actual channel activity.
- ADPCM meters show the original channel signal independently of the user-set gain in the
  volume view. Display sensitivity was increased so small ADPCM-B changes are easier to see.
- The channel view supports 25%, 50%, 75%, and 100% sizes, cycling from the default 75% in the
  order `75 → 100 → 75 → 50 → 25 → 50 → 75`. The dashboard time display and progress bar
  shrink and grow accordingly.
- The chips used by the current song are always shown below the progress bar. When the same
  chip is used in multiple instances, they are combined, e.g. `2xYM2610`, `3xDCSG`.

### v0.1.3

- Built the player dashboard using pixel sprites from the Windows original: play, stop, pause,
  previous/next, rewind/fast-forward, repeat/random, and channel, volume, playlist, song
  information, and 50% zoom-out buttons.
- Added current time/total length/loop position displays based on the Windows original font,
  and a progress bar, to the dashboard. A master volume fader ranging from `-60 dB` to
  `+20 dB` sits to the right of the file name area.
- The song information view shows GD3 metadata and lyric events. The channel view orders VGM
  chip panels as FM → SSG/PSG → PCM, and sub-outputs of Yamaha composite chips follow the same
  order.
- The volume view shows each chip instance actually used, and separable outputs such as
  FM/SSG/ADPCM/PCM are controlled with independent sliders. This includes YM2612 FM/ADPCM and
  Y8950 FM/ADPCM.
- MDX shows OPM (YM2151) and PCM8/PDX configurations separately, and keeps attempting OPM
  playback even if the PDX cannot be found. MDX driver loops end on the same basis as VGM, a
  maximum of 2 loops.
- For VGM 1.01 and earlier headers with a single FM clock, the actual command stream is
  analyzed so that only the one correct chip among YM2413/YM2612/YM2151 is initialized. This
  prevents YM2612 and YM2151 panels from appearing alongside YM2413 songs, and header fields
  specific to later versions are read safely under version conditions.
- Dropping a directory from Finder finds music files with supported extensions, including in
  subfolders, in stable name order and adds them to the playlist. Dropping on the playlist
  area appends; dropping on the dashboard replaces the current list.
- The macOS top app menu and Dock name are unified as `MDPlayer4Mac`. Even when running outside
  an `.app` bundle, such as with `dotnet run`, the AppKit bridge sets the same name and the
  Windows original main icon, and the GitHub Release `.app` bundle includes
  `MDPlayer4Mac.icns`.

## What works now

- Real-time playback using Core Audio Audio Queue
- Playback buttons and chip channel display using Windows-version sprites
- Channel view, volume view, dedicated playlist view, song information view
- Per-chip/per-output-block volume control and master volume
- Multi-file playlist, file and folder drag and drop, multi-selection and deletion
- Automatic next-song playback, repeat/random playback, playback speed control, auto play
- Channel view size switching: 25% / 50% / 75% / 100% (default 75%)
- Windows-version settings window layout and About screen. Currently the settings actually
  wired up center on Output.
- Pushing a tag automatically builds the macOS app for Apple Silicon and uploads it to GitHub
  Release.

## Supported formats

The extensions that can be opened or dropped in the UI are as follows.

```text
.vgm  .vgz  .xgm  .xgz  .sid  .mnd  .zms  .zmd
.mdx  .mdr  .mdl  .mub  .muc  .mml  .m  .m2  .mz
.mus  .o  .ox  .oy  .mgs  .msd  .bgm  .nrd  .ndp  .mid  .rcp  .rcs
.opi  .ovi  .ozi  .mpi  .mvi  .mzi
.nsf  .gbs  .hes  .s98  .ay  .zgm
.wav  .aif  .aiff  .mp3  .m4a  .aac  .flac  .ogg
.m3u (playlist)
```

- `.vgz` and `.xgz` are handled as gzip-compressed VGM and XGM respectively.
- For old VGM 1.01 and earlier files, the command stream is checked so that only the chip
  actually used among YM2413/YM2612/YM2151 is initialized.
- MDX is an X68000-family format. If a PDX is found in the same folder, it is used for
  PCM8/ADPCM playback; even without a PDX, playback of the OPM (YM2151) part is still
  attempted.
- `.mdr`/`.mdl` are MoonDriver OPL4/OPL3 formats. A `.pcm` file with the same name is used
  along with them if present. `.mml` is PMD source and `.m`/`.m2`/`.mz` are PMD compiled data.
  `.mus` is MUAP98 source and `.o`/`.ox`/`.oy` are MUAP98 compiled data.
- `.mgs` is the MGSDRV format. Because MGSDRV permits unmodified redistribution
  (`licenses/MGSDRV/`), placing `MGSDRV.COM` in `macos/MDPlayerUI/Assets/Drivers/` includes it in
  the app bundle. If it is not in the bundle, the file in the user Drivers folder or the one
  specified in Setting → Other → MGSDRV is used.
- For formats that need an original driver or ROM (MuSICA, NDP, FMP, ZMUSIC, OPL4's
  `yrw801.rom`), place the files in `~/Library/Application Support/MDPlayer4Mac/Drivers/`. The
  Windows version looks next to the exe, but on macOS that location is inside the signed app
  bundle, so a separate folder is used. You can open the folder and check whether each file was
  found under Driver files in the Other tab of the settings window.
- `.bgm` is MuSICA compiled data and requires `KINROU5.DRV` to be specified in Setting → Other.
  `.msd` is MuSICA source, so a `.vcd` with the same name is read along with it if present, and
  `KINROU4.COM` must also be specified for on-the-fly compilation. Due to licensing, neither
  file is bundled with the app or redistributed.
- `.ndp` is the format of NDP, an MSX PSG driver. The original `NDP.BIN` must be placed in the
  user Drivers folder; it runs on the same Z80/MSX host as MGSDRV and outputs to AY8910.
- `.opi`/`.ovi`/`.ozi` are PC-98 FMP compiled data and `.mpi`/`.mvi`/`.mzi` are FMP source.
  The original `FMP.COM` runs in the PC-98 (80286) emulator Nise98 and outputs to YM2608+PPZ8.
  Source formats are compiled in memory with `FMC.EXE` before playback. Both files must be
  placed in the user Drivers folder; since Nise98 emulates PPZ8 directly, `PPZ8.COM` is not
  needed. PVI/PZI PCM files are looked up in the same folder as the song file.
- `.nrd` is the NRTDRV format; the YM2151 (one or two)/AY8910 combination used by the file is
  configured automatically.
- `.mid` is a Standard MIDI File, `.rcp` is an RCP sequence, and `.rcs` is an RCP+PCM8 sequence
  format. Output goes to the macOS built-in DLS General MIDI synthesizer. `.cm6`/`.gsd` and
  companion `.rcp` files referenced by RCP/RCS are found automatically in the same folder.
  External MIDI devices and VST routing are not yet available.
- `.wav`/`.aif`/`.aiff`/`.mp3`/`.m4a`/`.aac`/`.flac` are decoded with macOS AudioToolbox
  (ExtAudioFile), and output sample rate conversion is also handled by the system. `.ogg` is not
  decoded by macOS, so it is read with NVorbis. The Windows version's `.wma` is not supported on
  macOS. EngineSmokeTest, which runs outside macOS, reads only WAV and OGG. Using
  `macos/tools/audio-file-smoke.sh`, generated test tones can be decoded in each format to check
  length and pitch (the audio-files job in CI).
- `.m3u` is a playlist file, not an audio file. It reads `#EXTINF` titles and relative/absolute
  local paths; network URLs are not currently supported and are skipped.
- `.mub` is MUCOM88's compiled data format, and `.muc` is compiled in memory to the same format
  at playback time. For MUC files that specify external data such as `#voice`, `#pcm`, or
  `#@pcm`, those files must be placed in the same folder as the MUC.
- MUCOM88 portamento takes its length after the first note: `{c4>c}`. The `{c>c}4` form used in the
  MUCEdit manual (length after `}`) is moved to `{c4>c}` in memory just before compiling, so both
  play the same. The MUC file itself is not changed, and a first note that already has a length is
  left as written.
- Even for formats in the supported list, not every driver and chip combination is complete.
  Combinations that cannot be loaded are shown as errors in the status bar.

## Using the player

When a file is first opened, the channel view appears. A playlist about 3.5 rows tall is
attached below the channel view and volume view, and pressing the playlist button switches to
a dedicated list screen 20 rows tall. With more than 20 songs, the list scrolls.

- Dropping files or folders on the playlist area appends them to the end of the current list.
- Dropping them on the top dashboard replaces the current playlist with a new list.
- Dropping a folder searches subfolders too and adds supported files in name order.
- Opening or dropping an `.m3u` expands and adds the supported music files in the list.
- Standard list editing is available with Cmd+A, Shift/Cmd selection, Delete/Backspace, and
  Escape.
- Pressing Stop does not close the current channel view.
- When a song ends, the next song plays. The repeat button repeats the current song; pressing
  it once more switches to random playback.
- A short press on Play resets playback speed to 1x and then plays or resumes. Holding Play for
  2 seconds or more, or a Force Click, toggles auto play on and off.

### Keyboard shortcuts

The top button row maps to `Q W E R T Y U` and the bottom button row to `A S D F G H J`, from
left to right.

```text
Q Stop       W Pause      E Previous   R Slow
T Play       Y Fast       U Next
A Open       S Playlist   D Information  F Volume
G Channel    H Zoom       J Loop / Random
```

## Channel view and volume view

The channel view shows the chips actually used in the VGM in the order FM → SSG/PSG → PCM. If
there are multiple instances of the same chip, all are shown, and the dashboard groups them,
e.g. `2xYM2610`. Yamaha composite chips show outputs of different natures, such as FM, SSG,
ADPCM, and PCM, separately.

The volume view shows only the chip instances used in the current song. Independent outputs
such as FM/SSG/ADPCM/PCM can each be adjusted where possible. Reset Volume restores the default
volume stored in the file, and formats without a default use 0 dB.

## Settings

Open with the Setting icon at the bottom right of the dashboard.

- `Output` configures the Core Audio default output, buffer latency (25–500 ms), pre-playback
  wait time, and sample rate. Changes apply from the next playback.
- Sound, per-chip emulation, MIDI, playlist, network, other, and About keep the Windows
  version's layout and item names as much as possible.
- Features not yet available on macOS, such as SCCI/C86CTL, ASIO/WASAPI, VST, and real-time
  MIDI I/O, appear disabled. This is so that unimplemented settings do not affect playback.

## Development environment

- macOS
- Xcode Command Line Tools (including `xcrun clang`)
- .NET SDK 9.0.300 or later (10 recommended). Avalonia 12's XAML source generator requires
  Roslyn 4.14, so it does not build with SDK 8 (the target framework remains `net8.0`)
- Internet connection for the initial NuGet restore

`MDPlayerUI` uses Avalonia. Because Force Click does not arrive through Avalonia's regular
pointer events, `MDPlayerUI/Native/ForceTouchMonitor.m` is built into a small Apple Silicon
dylib and used alongside it. This library also sets the AppKit app name and Dock icon when
running in development.

## Build and run

Run from the repository root.

```bash
dotnet build macos/MDPlayerUI/MDPlayerUI.csproj -c Release
dotnet run --project macos/MDPlayerUI/MDPlayerUI.csproj -c Release
```

To check only the playback engine without the GUI, run the following.

```bash
dotnet run --project macos/LivePlayer/LivePlayer.csproj -c Release -- /path/to/song.vgm
```

The smoke test that renders VGM to WAV is as follows.

```bash
dotnet run --project macos/EngineSmokeTest/EngineSmokeTest.csproj -c Release -- /path/to/song.vgm /path/to/output.wav
```

### Building in Xcode

Open `macos/MDPlayer4Mac.xcodeproj` and run the `MDPlayer4Mac` scheme (⌘R). The app itself is
.NET/Avalonia code, so Xcode does not compile it directly. The app target's `Build .NET App`
build phase calls `macos/Xcode/build-app.sh` to run `dotnet publish` (self-contained, single
file, `osx-arm64`) and places the output in `MDPlayer4Mac.app/Contents/MacOS`. Xcode handles
`Info.plist`, the icon, and signing. The bundle layout is the same as the release workflow.

- Prerequisites: .NET SDK 9.0.300 or later (10 recommended), submodules
  (`git submodule update --init --recursive`)
- Xcode's Debug/Release configurations are passed straight through as
  `dotnet publish -c Debug/Release`
- `dotnet` is searched for in `PATH`, `/usr/local/share/dotnet`, `/opt/homebrew/bin`, and
  `~/.dotnet`, in that order. If it is elsewhere, set `DOTNET=/path/to/dotnet` in the scheme's
  environment variables
- Signing defaults to "Sign to Run Locally" (ad-hoc). To sign with a Developer ID, just change
  the target's Signing settings; the dylibs inside the bundle are signed with the same
  certificate
- LLDB cannot debug .NET managed code, so the scheme runs the app without a debugger. If you
  need to debug, attach the VS Code or Rider .NET debugger to the running process
- Change the app version in the target's General tab (`MARKETING_VERSION`)
- When changes under `macos/` are pushed to a branch, `.github/workflows/xcode-build.yml` builds
  Debug/Release with `xcodebuild` on a macOS runner, and also verifies the bundle layout and
  signing and performs a 15-second launch check

## Releases

Pushing a `v*` tag makes `.github/workflows/release-macos.yml` build a self-contained app for
Apple Silicon and attach it to the Release.

```bash
git tag v0.2.3
git push origin v0.2.3
```

The output is `MDPlayer4Mac-osx-arm64.zip`. Unzipping it yields a single `MDPlayer4Mac.app`
bundle. The .NET runtime, Avalonia, the Force Touch dylib, and the app icon are included in the
bundle.

## Gatekeeper notes

Current builds have only ad-hoc signing applied in CI. Apple Developer ID signing and
notarization have not been done yet, so when first launched on another Mac, Gatekeeper may show
a warning that the developer cannot be verified. This is expected behavior with the current
distribution method.

Only use `MDPlayer4Mac-osx-arm64.zip` downloaded from this repository's
[GitHub Release](https://github.com/ToughkidDev/MDPlayer4Mac/releases). Do not apply the methods
below to apps whose origin is uncertain.

The most recommended way to launch is as follows.

1. Unzip the ZIP and move `MDPlayer4Mac.app` to the `Applications` folder.
2. Open the app once to trigger the Gatekeeper warning.
3. Go to **System Settings → Privacy & Security** and click **Open Anyway** near the bottom of
   the screen.
4. In the confirmation dialog that appears again, click **Open** and enter your macOS login
   password.

This approval is stored for that app only, so afterwards you can launch it by double-clicking
like any other app. The `Open Anyway` button is shown for about an hour after the first launch
is blocked.

Only if the button does not appear in System Settings, after confirming that the file is the one
from GitHub Release, you can remove the quarantine attribute in Terminal.

```bash
xattr -dr com.apple.quarantine "/Applications/MDPlayer4Mac.app"
```

This command applies to that one app only and does not turn off Gatekeeper itself. However,
since it directly removes the security flag on a downloaded file, do not use it on apps whose
origin cannot be verified or apps that show an `is damaged` or malware warning. In such cases,
delete the app and download the Release ZIP again.

Apple's latest guidance also recommends allowing only that app via
**Privacy & Security → Open Anyway** after a first attempt.
[Apple Gatekeeper guide](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac)

## Known scope and next tasks

- Apple Developer ID signing and notarization
- SCCI/C86CTL real chip output, Windows-only ASIO/WASAPI output
- VST hosting and real-time MIDI passthrough
- LHA archive handling
- Expanded real-song regression tests per format and chip combination

`Z80dotNet 1.0.6` is a package targeting .NET Framework, so the `NU1701` warning remains. Build
and playback currently work, but it would be better to switch to a net8.0/netstandard
compatible package later.

## MUCOMSX MSX extended playback

`msPb0100` and `msPb0101` are detected separately to play MSX-MUSIC, internal PSG, and SCC+ SSG.
In-memory compilation of MUC containing native `MSX` parts is also supported. The behavior and
approximation limits of `@I`, `@W`, `pe`, and `#PCMInvert`, and the regression tests, are
documented in the [MSX playback guide](docs/MSX-PLAYBACK.md).

## License

Follows the licenses of the original MDPlayer and the external code included in `MDSound/`.
When adding or distributing new code, check the copyright of the original files and the
GPL-family terms.

[mucomDotNET](https://github.com/kuma4649/mucomDotNET) is included as a Git submodule for MUB
playback and [PMDDotNET](https://github.com/kuma4649/PMDDotNET) for PMD playback, and the GPL-3.0
licenses and copyright notices of both projects are followed. For MoonDriver and MUAP playback,
the sources of [MoonDriverDotNET](https://github.com/kuma4649/MoonDriverDotNET) and
[muapDotNET](https://github.com/kuma4649/muapDotNET), respectively, are included as Git
submodules. These two repositories show no separate license file on GitHub, so be sure to
re-check the copyright and distribution terms with the original author and repositories before
distribution.
