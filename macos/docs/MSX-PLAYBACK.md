# MDPlayer4Mac native core split (2026-10-26)

`macos/MucoNative` builds `MDPlayer.MucoNative.dll`, independently of the
MSX toolchain and the MDPlayer UI/audio engine. The in-process C# compiler and
session sequencer own channel mapping, bounded profile reads and ordered chip
delivery. The app supplies chip synthesis, an asset resolver and a sample clock.
No MSX executable, overlay, game ROM, disk or CPU emulator is used by this core.
Ordinary OPNA MUB/MUC remains on the existing adapter path.

The native core now implements the musical PN1–PN6 profiles (`msPb0103` through
`010A`) as well as the previous `0100`–`0102` paths. New two-letter `?` commands,
historical `?`/`@` aliases, shared waves/phrases, envelopes, signed detune,
pitch/instrument tables, effect lifecycle, fade and interpolation are compiled
and sequenced in process. OPNA A–K retains its existing syntax and can play
alongside SCC+/PSG/OPLL. See [the command and core guide](../MucoNative/README.md).

PN6 `?XTn,1` changes the live track state directly, while compact
`?XTn,2` additionally preserves dormant note phases in rhythm mode. Returning
to melody does not restore cancelled vibrato or detune. The single-operand
legacy `?XTn` and `?k`/`@k` aliases keep their existing saved-state semantics.
This distinction fixes the PSG pitch drift found in Special PSG selectors
`P20_03`, `P30_05` and `P38_02`; their MUC and MUB bytes remain unchanged.

The new tests compare compiled musical bytes and bounded per-tick chip output;
the engine suite also renders a newly compiled four-chip score and exercises
error/restart. The earlier 41-case legacy migration comparison remains separate.
Bounded runtime parity does not establish full original-game or listening parity.

# Local msPb0102 update (2026-10-20)

The local build additionally supports `msPb0102` capability value 5. GitHub's
latest-release API, checked on 2026-10-26, identifies v0.2.3 as the published
release. The native-core split above is a local source update after that release.

SCCA–SCCE are five independent native SCC+ note parts (directory parts 22–26),
separate from OPNA and MSXA–MSXJ. The driver uses an independent SSG-D work area
per part/page, the OPNA pitch table and Work's period rounding; volume is linear
0–15. Wave uploads use SCC+ port 4, including the independent fifth wave. The
legacy 0100, 0101, raw and ordinary MUB/muPb paths remain separate.

Opening a MUC accepts signed 32-sample inline `@Wn:{...}` definitions and
optional `#sccvoice` SCWD v1 banks. Inline definitions override bank IDs; duplicate
inline IDs, undefined IDs, invalid banks, mixed SSGExtend/SCC5, unsupported
capabilities and reserved header bytes are rejected. An explicitly named missing
or corrupt bank remains an error even with inline definitions. The in-memory
compiler emits the exact inline-wave instruction inside the musical stream;
repeat and loop offsets are calculated by the compiler, never patched by a byte
search. MUC compilation has no converter/toolchain executable dependency.

Only primary-chip Timer-B writes are allowed in SCC5's extended register control
instruction. Noise, hardware envelopes, raw SCC and pan extensions cannot claim
this dedicated target. End/error mutes all five SCC channels. SCC-only scores do
not register a silent OPNA/OPLL/PSG audio target.

Regression coverage includes simultaneous five-channel waves/linear volume,
independent fifth wave, page 9, multi-select, macros/repeat endings, continuous
loops, SCWD CRC/overrides/errors, profile bounds and ordinary SSG compiler mode
isolation. Run the existing MsxPlaybackTests command below.

Player verification also found a converter defect: restoring the constant
software envelope at L while emitting a tie can put the envelope into release.
MUCCONVERTER now starts a note at that restoration point, without a hardware
key-off; regenerate affected note MUC/MUB files. Playback equality between MUC
and MUB alone cannot detect a shared conversion defect.

# MUCOMSX MUB / MUC playback (2026-10-17)

## Native-only mixer chip detection (2026-10-16)

A native msPb score may contain an OPNA page used only to set Timer B. The MUC
adapter's default `A t225` is one example. These sequencing commands still run,
but no longer register a silent YM2608 in the audio mixer. MUC and MUB playback
therefore use the same gain when their sounding PSG/SCC/OPLL targets match,
regardless of the redundant timer page.

The reader excludes only fully terminated pages containing Timer-B writes and
rests, in both direct and explicit primary-chip/port-0 encoding. Notes, other
register writes (including CSM), other indirect writes, unknown
commands and incomplete instructions retain OPNA conservatively. SSG D/E/F
software-envelope opcodes are distinguished from FM register-write opcodes. Genuine mixed
OPNA/native music, SCC-SSG noise/envelope fallback and legacy MUB8/muPb loading
keep their existing routes. The change does not rewrite tempo, notes or loops,
and does not alter MDSound's gain rules for other formats.

Loop entry points must also fall on recognized instruction boundaries before a
page can be excluded. A loop into an operand, an unterminated one-byte page,
or any unclassified entry retains OPNA. A score containing only control/rest
pages and no audio targets retains a single silent YM2608 render clock, so it
still loads, advances and ends. This fallback is never added alongside native
PSG, OPLL or SCC targets and therefore does not change their normalization.

## v0.2.2 compatibility updates

The 2026-10-13 local build fixes the forty rhythm differences reported during
the recommended-MUB refresh. Extended OPNA G pages used an implicit instrument
mask of 63, so rests in an otherwise unselected G page cut drums triggered by
authored `y16` writes in other parts. MUCOMSX starts its rhythm selection at zero.
The overlay now does the same for OPNA; explicit `@63` and other masks still work.
OPNB ADPCM-A retains its original initialization. A mucomDotNET score relying on
an implicit all-drums selection should explicitly specify `@63`.

SSG wave uploads now use common SCC port 0, matching the latest Work driver and
its SCC fallback. Identical channel wave RAM is cached, but gain/noise updates
are always emitted. The cache owns its data and cannot alias caller buffers.
The emulator still chooses SCC+ for the SSG profile; D/E/F use only the three
wave channels common to SCC and SCC+.

Raw transport discovery now walks FM3/portamento/envelope/morph/volume-mode,
automatic-pan and TL-LFO command operands correctly, and distinguishes SSG
wave/pan operands from FM commands. Marker-like bytes inside operands do not
declare a chip. Existing invalid-format checks remain in force.

MSX-specific MUC compilation primes Timer-B 225 before music, matching Work's
default; authored tempo commands at tick zero override it. Interleaved selectors
such as MSXA0B0 now participate in the header-order check. Ordinary non-MSX MUC
compilation retains upstream defaults: source playback is not promised to match
Work when platform defaults differ. Specify tempo/clock explicitly for comparisons.
Optional UTF-8 BOM and CP/M EOF framing are accepted without rewriting MML or
Shift-JIS bytes, so a first-line MSX declaration cannot silently disappear.

Verification after the Work RhythmMute repair: twenty actual latest-toolchain
MUBs and their MUC sources, 1,283,647 assertions, thirty-six assembled-driver differential vectors, and all
256 timer periods. The recommended corpus comparison covers 888 MUBs (828 MSX,
60 OPLL) for up to the first 600 logical ticks, excluding platform startup and the
last boundary tick. The previous forty rhythm discrepancies become zero, with
no new register-state/event discrepancies in that compared interval. These are
not whole-song bit-identical audio or real-hardware claims.

These compatibility changes and the native-only mixer fix are included in
v0.2.2. Its regression suite passes 1,284,281 assertions, including 634 focused
chip-detection/reload checks and complete finite playback of twenty MUB and
twenty MUC fixtures. After the mixer stability follow-up, all twenty MUB fixture
captures retain identical PCM and timing to the preceding mixer-fixed build.

The separately confirmed Work RhythmMute defect is now repaired in the Work
compiler and MUCPLAY session restoration. The tag no longer sets the native
target-layout bit, and session playback restores its own stored mute mask rather
than interpreting that bit as mask presence. MUC2MUB, MUCPLAY and MUCEDIT exports
are byte-identical in the final single-boot 32MiB FAT16 / FS-A1WX / Z80 / 512KiB /
MAKOTO test, including error recovery and complete mapper release (432KiB peak).
See Work validation/20261013-rhythm-mute-fixed-native-v5/result.json.

MDPlayer already applies the same note-level rhythm mask: no extra sequencer
patch is necessary for these corrected files. New tests cover all 64 masks,
unblocked key-offs, and preservation of authored direct `y16` writes, which
bypass note-level mute in both implementations. Three complete finite models
(plain, note-muted and raw-register control) have matching Z80/MDPlayer OPNA
settled states and ordered events after initialization and before final shutdown.
The editor's VGM retains four masked triggers and validates its file/sample extent.
Previously damaged MUBs must be recompiled; a player cannot recover missing notes.

Independent MUC compilation exposed a separate compiler-overlay defect:
`pm1 p4..p6` emitted the wait only after the last selected drum's pan command.
The overlay now emits a validated wait for every expanded F8 command, using
the current length when omitted. 768 combinations match explicitly expanded
packed-pan sources. The finite RhythmMute MUC and Work MUB now end together;
ordinary authored `y16` control and static p1..p3 semantics are retained.

The following sections retain earlier format and verification information.

MDPlayer4Mac now recognizes the MUCOMSX toolchain's MSX containers separately from
ordinary MUCOM88 MUBs. Opening a `.mub` or `.muc` uses the existing player UI.

| Format / feature | Playback |
| --- | --- |
| `MUB8`, `MUC8`, `muPb0100` | Existing MUCOM playback path |
| `msPb0100` | MSX-MUSIC (6 melodic channels + drums) and MSX PSG |
| `msPb0101`, capability byte 26 = 2 | OPNA D/E/F tones on SCC+; OPNA noise/envelopes retained |
| `msPb0101`, capability byte 26 = 3 | SCC+ SSG plus native MSX parts |
| Raw v1 native register transport | YM2413 user instrument / 9-channel mode and PSG register writes |
| Raw v2 native register transport | Declared OPLL, direct PSG, SCC/SCC+ and clock-scaled OPNA SSG writes |
| `#SSGExtend On`, `@I0..9`, `@W0..255`, `peL,R` | SCC+ waveform and mono gain; page-local state |
| `#PCMInvert On/Off` | K relative octave direction during MUC compilation; no runtime inversion |

Native MUC `MSXA..J` declarations are compiled through the second sequencing group,
then explicitly routed to MSX targets. Ordinary dual-OPNA MUC remains on its
original route. `MSXK` has no ADPCM target and is rejected. Native FM3 special mode
is rejected. External `#voice` / `#pcm` files must be available alongside the MUC.

The mapper follows the current toolchain's integer rounding, ROM preset selection,
drum retriggering and I/O mask preservation. This reproduces the **toolchain's MSX
approximation**, not the original four-operator FM sound: ordinary native FM uses
OPLL presets and RIM has no equivalent drum. Raw native mode can directly program
the OPLL user instrument and all nine melodic channels.

SCC+ uses mono audio, 32 samples and linear 4-bit volume. A 64-sample `@W` is reduced
by averaging neighboring signed samples. Pan is mono gain, not stereo positioning;
automatic pan becomes half gain. Hardware S/m envelopes fall back to OPNA square
wave/noise. These limitations match MUCOMSX's SCC+ profile.

Unknown versions/capabilities, reserved 0101 bytes, invalid directory references,
unsupported parts and truncated blocks are rejected before upstream allocation.
Invalid waveform/pan commands stop playback and mute native/SCC+/OPNA outputs.

## Raw v2 control transport

The raw marker is the native-group register command at address 191. Value 1
selects raw v1. Raw v2 has bit 4 and bit 0 set; bit 1 declares OPLL, bit 2 SCC,
and bit 3 selects SCC+ (requires bit 2). The loader reads markers at compiled
instruction boundaries to initialize the required SCC emulator before playback.
Raw SCC and the SCC+ SSG extension cannot claim the same target.

Registers 142..147 select SCC ports 0..5 and the starting address. Register 148
writes data and advances the address modulo 256. SCC+ port 4 preserves the
independent fifth waveform. Registers 149..162 carry source OPNA SSG registers
0..13; 163/164 set the Q0.16 PSG clock factor (initially 58741). Tone, envelope
and noise periods use the Work driver's half-up rounding and source-register
cache. Direct PSG 128..141 and raw v1 OPLL commands retain their existing route.

Native raw v1/v2 streams also use the Work application's integer sample clock:
`(((256 - TimerB) * 203 + 16) / 32) * 2` at 44.1 kHz. An ideal OPNA timer
accumulates a different duration when a converter changes Timer B frequently.
`MsxRawTimer` preserves phase across tempo changes and status acknowledgements;
ordinary MUC/MUB and native melodic MML retain their existing timer path.
The 256 reference periods are captured by executing the shipped MUCPLAY routine.

These fixes are included in v0.2.1. The v0.2.0 download predates them.

## Verification

The synthetic corpus contains fifteen actual assembled-toolchain MUB outputs and
their MUC sources, including raw v2 PSG-only, SCC-only, SCC+ channel 5-only,
clock-scaled SSG and mixed-chip streams. Tests render each MUB and MUC to audio,
check nonzero signal, finite completion and driver errors. They reject every
truncated directory/payload prefix, invalid raw flags and false markers inside
command operands, exercise command errors and verify K-only PCMInvert behavior.

36 differential register vectors are generated by executing the **actual assembled
MSX driver** in a Z80 emulator, including 2,900 random writes, raw/drum sequences,
SCC address auto-increment and SSG rounding at four clock ratios. Tests compare
complete ordered register lists, preserving repeated envelope-shape writes.
SCC selector and SSG source state survive repeated markers and song loops.
Stop mutes all five SCC channels and releases the raw SCC route.

The 2026-10-11 local run passed 26,009 assertions, including an optional real-song
corpus: seven source VGMs and their converted MUC/MUB files. SCC/SCC+ waveform RAM
and periods matched the source at song end; the looping song completed three
laps. The commercial files are local validation inputs and are not bundled.
These checks establish register handling and audible playback, not identical
whole-song audio: source-chip clocks, mixing and converter timing still matter.

The follow-up album run covered all 222 MUC files in 44 albums from
`mucsMSXmspb`: 151 finite songs and 71 looping songs played through three laps.
All produced audio without driver errors or clipping. Their 818,635 ordered
PSG/SCC/SCC+ source commands matched the actual sequencer raw sink, including
loop re-entry, macro expansion and SCC address increments. Earlier, 67 songs
exceeded 100 ms of duration difference (maximum 557 ms); the native raw timer
removed that accumulation. Final duration differences were at most 62.2 ms,
including the converter's leading rest and the 2,048-frame render block.
This is not a whole-song bit-identical audio assertion: mixing, clocks and
intentionally omitted non-MSX chips remain outside that comparison.

Run a local album manifest (paths to source music are not committed):

```sh
dotnet run --project macos/MsxPlaybackTests/MsxPlaybackTests.csproj -c Release -- --album /path/to/manifest.json /path/to/results.json
```

Each row supplies `muc`, `loop`, `input_samples`, `loop_samples`, and optionally
`source_events`, a JSON path with `initial_writes` and `events` arrays. Events
are `[chip, register, value]` with OPLL=0, AY=1, SCC=2, source SSG=3.

The synthetic regression run passed 1,278,232 assertions, including the 256
actual Z80 timer periods, changing tempo and acknowledgement phase retention.

Run from the repository root:

```sh
dotnet run --project macos/MsxPlaybackTests/MsxPlaybackTests.csproj -c Release
```

The committed fixtures need no toolchain or Python installation. To regenerate
against a newly built toolchain, install Python's `z80` package and run:

```sh
python3 macos/tools/generate-msx-playback-fixtures.py /path/to/MucoMSX_Toolchain_Work
```

`manifest.json` records compiled file hashes; `provenance.json` records the MSX
driver hash. Music/voice fixtures are synthetic. Passing these tests does not
establish compatibility with every existing album or real hardware output.

The compiler overlay also corrects upstream PCMInvert leaking into non-ADPCM
parts; global `#invert` is unchanged.

The [driver overlay](../MucomDriver/README.md) explains source provenance, the pinned
submodule dependency and how to update it without overwriting local submodule edits.

### SCC album regression fixes (2026-10-20 local msPb0102 build)

The original-MUC compiler now merges tied notes only when the previous note bytes are adjacent and belong to the same chip/part/page. A tie after a repeat end can otherwise overwrite its jump operands and crash later playback. The msPb packer also removes unused trailing pages while preserving authored page numbers and the required base parts; empty pages no longer consume the 120 KiB music budget.

These changes were exercised with original MUC compilation and playback of the converter-produced MUB, using the coherent six-assembly Release reference. Corpus comparison records natural completion or three source passes, exact MUC/MUB output PCM, and separate source SCC/PSG audible-state checks. This does not claim waveform identity to the source VGM or GUI/real-time listening coverage. The converter additionally restores compile-time octave after `/` repeat exits and preserves byte-level normal-SCC/fifth-wave aliasing.

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
