# MDPlayer4Mac Muco native core

MDPlayer4Mac owns this managed C# compiler and musical sequencer. The project
builds independently of the MSX toolchain, MDPlayerCore and MDSound. It has no
MSX CPU, COM/overlay loader, mapper, Python, game ROM or disk dependency. The
host supplies companion assets, synthesis and the audio sample clock.

`NativeMucCompiler.Compile` compiles MUC in memory. `NativeMucomSession` supplies
ordered `NativeChipWrite` events through `AdvanceSample`, with isolated song
state, status, loop count, diagnostics and explicit stop/disposal. Asset streams
are disposed by the compiler/driver. No fallback to expanded register timelines
is used. Existing public `MsxMubProfile`/`MsxSoundMapper` types remain available.

## Formats and channels

* Existing `msPb0100`–`0102`, raw transports and old SCC5 MML retain their paths.
* `msPb0103`–`010A` contain bounded PN1–PN6 musical objects. The core reads and
  sequences notes, shared phrases/resources, repeats and effects directly.
* PSG: `MSXD`, `MSXE`, `MSXF`; SCC+: `SCCA`–`SCCE`; OPLL melodies:
  `MSXA/B/C/H/I/J`, rhythm: `MSXG`. Shared definitions use `PSGS/SCCS/OPLS`.
* Ordinary OPNA `A`–`K` can play alongside these parts. Its MML and managed
  MUCOM compiler/driver remain unchanged. Extra OPNA/OPNB/OPM groups are reserved
  by the packed mixed container and rejected if they contain music.

No `#msx compact` switch is required. An explicit `?XU` selects compact length
units for its MSX channel; ordinary MUC lengths and native extensions can coexist.
An ordinary MSX row without new extensions retains the previous compiler path.

## MSX commands

The two-letter `?` names are accepted together with existing single-letter `?`
and historical `@` aliases. Numeric `@N` keeps its existing voice meaning. These
extensions do not rename or reinterpret OPNA commands. Channel restrictions and
operand limits are checked; unsupported commands produce diagnostics.

| Commands | Musical function |
|---|---|
| `?EP`, `?EA`, `?ET`, `?EN`, `?EL` | Envelope profiles, attack, initial target, onset attenuation, floor |
| `?DB`, `?DW`, `?DR`, `?DS` | Legacy/signed detune, disable, byte/word sweep |
| `?MV`, `?MW`, `?QR`, `?QP` | Vibrato/table pitch program, delay, gate controls |
| `?NP`, `?GS`, `?GH`, `?GL` | Shared PSG noise policy, shape restart, independent period bytes |
| `?WD`, `?WS`, `?WM`, `?WP` | SCC waves, aliases/SCWD bank import, morph, sharing policy |
| `?WI`, `?WF`, `?WE` | Iterative interpolation, policy flags, envelope-phase waves |
| `?IR`, `?IU`, `?IT`, `?IS`, `?ID`, `?IB` | OPLL ROM/custom voices, drum setup, sustain and drum hits |
| `?FD`, `?FC`, `?FR`, `?FJ`, `?FB` | Shared phrase definitions, calls, returns, jumps, repeats |
| `?XD`, `?XM`, `?XI`, `?XS`, `?XP`, `?XT`, `?XU` | Shared effects and compact musical streams |
| `?KT`, `?IM` | Pitch-table and instrument-program selection with per-note latching |
| `?XH`, `?XC`, `?XR`, `?VF` | Effect hold, cancel/group end, restart and global fade |

Global resources use `?WDn={32 signed samples}`, `?WSlocal,bank,1` with
`#sccvoice`, `?IUn={8 OPLL bytes}`, `?ITn={6 drum bytes}`, and `?KTn`/`?IMn`
word tables. `?IR`/`?IU` explicit selection is used on ordinary OPLL MUC rows;
compact OPLL also retains its historical numeric `@N` instrument encoding.
PSG hardware-envelope shape writes and repeated SCC gate/wave transactions are
events: adapters must preserve them even when values repeat.

## Validation boundary

The current compatibility baseline is MucoMSX 2026-10-26 (msPb0100–010A;
PN1–PN6, with PN5/PN6 both in 0109 or mixed 010A). The app's About page names
this baseline separately from its published MDPlayer4Mac version.

The synchronization campaign uses 743 supplied MUCs and two synthetic mixed
controls. It checks compiler bytes, ordered chip events, natural end or an
explicit 6,000-tick bound, including gates, shape restarts and the independent
fifth SCC+ wave. Engine tests cover mixed playback, old MUC/MUB compatibility,
stop and error/restart. These are software comparisons, not full human
listening, complete original-game equivalence or an MSX hardware deadline
claim. Quarth 14 and TwinBee 16 retain their prior conversion limits.

```sh
dotnet run --project macos/MucoNativeTests -c Release -- macos/EngineSmokeTest/testdata/msx
dotnet run --project macos/MsxPlaybackTests -c Release
```

Trace probes are test-only entry points. The shipped core does not access the
reference toolchain or test captures. A successful build alone is not playback
qualification. Keep the UI, harness and native-core DLLs from one coherent build.

### Original-driver OPLL and PSG controls (PN6)

Compact OPLL melodies can select a twelve-entry ?KTid[,start] F-number window
(1..1023), with ?KT- restoring the ordinary table. Values at least 512 advance
the block after halving the F-number. PSG/SCC KT semantics are unchanged.
?QPvalue,1 starts fresh gate arithmetic for each OPLL note; mode 0 retains the
old accumulated counter. One-operand QP keeps the selected mode.

On MSXG, ?ITid,bias adds 0..15 to high-hat attenuation while preserving the
six-byte resource and existing one-operand default. ?ID-,length commits any
pending kit/volume and disables rhythm during the rest; numeric masks stay 0..31.
?XT0 clears saved OPLL type state and requires ?XT1 before pitched notes.

?QE0/1/2 is a new OPLL end-policy family: ordinary key-off (default), a keyed
frequency-zero end preserving the instrument (melody only), or final physical
state hold. Explicit stop/disposal silences the session. ?NP-,3 selects
one shared last-writer PSG noise latch without changing policies 0..2.

Declare global resources before musical channel rows. Existing supported
@/? aliases and OPNA syntax retain their paths. The MDPlayer core remains
an independent managed implementation; these controls require no MSX runtime,
ROM, disk, Python process or CPU emulator at playback.

`SCCE ?WG15` enables the explicit song-wide static-upload sample/gate policy; `?WG-` disables it. Only compact SCC routes with WP0 accept it. Default playback, OPNA syntax and existing aliases retain their semantics. Each static upload writes zero and then the shared gate mask to the chosen physical sample, without changing the real gate latch or logical voice resources. WM dynamic writes skip this policy. New playback instances reset it.

### MucoMSX 2026-10-26 synchronization

The native core accepts the current toolchain's musical msPb profiles through
010A, including PN6 and `?WG`. New two-letter commands retain existing `?`/`@`
aliases; OPNA MML remains on its established managed compiler/driver path.
The About page displays the compatibility date independently of app version.

Compact byte sweeps now rearm on the next duration boundary. Ordinary PSG `w`
writes the shared noise latch; `?NP` retains explicit ownership policies. A rest
after `?IM- ?XC` releases cancelled program ownership while preserving native
pitch phases. These distinctions are covered by regression cases.

For mixed MUC, braced FM definitions (`@N:{38 parameters}` and
`@%N:{25 bytes}`) are converted in memory to the managed compiler's existing
fixed-column voice format. Existing fixed-column definitions and external banks
retain their paths. A missing referenced FM voice still fails compilation; no
empty default bank is substituted. OPLL projection shares the same packing.
The on-disk MUC is not rewritten.

This macOS implementation has no MSX CPU, ROM, COM, mapper or external toolchain
process dependency during playback. See MucoNative/README.md and the native
regression executable for the validation boundary.

Compact channel routes are also established when shared phrase definitions
precede their main notes in a mixed ordinary/compact source. Definition-only
rows remain silent; a real following main stream must not become an empty route.

### Compact-row authoring fixes

Compiled scores that were accepted before are unchanged byte for byte (the 38 chip
extension MUCs of MUCOMSX's Samples compile to the same objects). Fixed playback:

* `?FB0 … ?FBn` now repeats on OPLL melody and `MSXG` routes; the OPLL player
  stopped at the loop mark (opcode `DE`) the compiler emitted.
* A rest on a `?XT4` effect track waits instead of stopping playback, and a rest
  on an `MSXG` `?XT64` track hits nothing instead of mask 12 (snare and tom).
* `?ID` hits apply a pending `?IT` kit, rhythm volume and `?IB`, as note hits do.
* A wave morph whose stride runs past the last wave wraps to the first wave
  instead of stopping playback.

Newly accepted source (previously compile errors):

* Compact rows take `|`, `[ ]n` (two levels, also across lines; labelled repeats)
  and `L` (the closing `!` jumps back to it). `/` is refused with a hint.
* Compact OPLL rows take `?IRn` (as `@n`) and `?IUn` (custom patch n).
* Songs whose compact rows are all PSG need no `?WD`; a silent wave 0 is added.
  A compact SCC row without waves names `?WD` in its error.
* Messages name the two-letter commands (`?IR`, `?WM` …) instead of internal letters.

`macos/MucoNativeTests/CompactRowTests.cs` covers these cases.
