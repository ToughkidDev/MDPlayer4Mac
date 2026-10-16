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
