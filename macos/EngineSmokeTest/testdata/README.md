# testdata

Hand-built, minimal VGM files used to verify `EngineSmokeTest` actually
produces correct audio (not just that the build compiles). All are valid
VGM 1.50 files with just enough header + a handful of register writes + a
single wait + end-of-sound-data command — small enough to read byte-by-byte
against the VGM spec if you want to sanity-check them yourself.

- `sn76489-tone.vgm` (76 bytes) — sets SN76489 (PSG) channel 0 to a fixed tone
  (frequency divisor 0x0ab) at full volume, waits ~0.4s, ends. Expected tone:
  `3579545 / (32 * 0x0ab)` ≈ 654.2Hz. Run it and check the output WAV's pitch
  roughly matches (e.g. a zero-crossing frequency estimate, or just listen to
  it — it should be a clean, steady beep, not silence or noise).

- `ym2612-fm-tone.vgm` (158 bytes) — sets up YM2612 (FM) channel 0 with
  algorithm 7 (all 4 operators additive, no feedback), MUL=1 on all operators,
  a fast attack / short decay envelope, frequency block=4/fnum=1000, keys the
  channel on, waits ~0.4s, keys off, ends. Expected: an oscillating tone with
  an audible attack ramp at the start (not a hard on/off click).

- `ym2151-tone.vgm` (107 bytes) — sets up YM2151 (OPM, arcade/X68000) channel 0
  with algorithm/CONNECT=7 (all 4 operators as parallel carriers), only
  operator M1 audible (the other three TL'd to max attenuation), KC=0x40/KF=0
  for pitch, a fast attack / sustain-at-peak envelope, keys the channel on,
  waits ~0.4s, keys off, ends. Verified (not just "doesn't crash") by loading
  the rendered WAV in Python and checking it's neither silent nor clipped
  (max amplitude ~4084/32767, 18427/18432 nonzero samples) and has a plausible
  single-frequency oscillation (~139Hz zero-crossing estimate over the middle
  segment) - this is the first of the second wave of chips added in
  VgmEngine.cs (YM2151/YM2203/YM2608/YM2610/YM3812/YM3526/YMF262/AY8910/
  YM2413/K051649/SEGAPCM), chosen as the one test case because it exercises
  the most different code path (real envelope generator, KC/KF pitch
  encoding, CONNECT-based operator routing) of the batch - the rest share
  the same simple single-instance wiring pattern and weren't each given a
  dedicated fixture (see macos/README.md for the full rationale).

- `sn76489-tone.vgz` (63 bytes) — the exact same bytes as `sn76489-tone.vgm`,
  gzip-compressed (`gzip.compress(data, compresslevel=9)`), to test
  `VgmEngine.DecompressIfGzip`. Confirmed byte-identical WAV output to
  `sn76489-tone.vgm` (`cmp` on the two rendered files matches exactly) -
  proof the decompression path doesn't alter the underlying VGM data at all,
  not just that it "runs without crashing".

- `nes-apu-tone.vgm` (211 bytes) - sets up the NES APU's pulse channel 1
  ($4000-$4003, VGM command 0xB4) with duty 10%, constant volume 15,
  length-counter-halt/envelope-loop set (so it doesn't need a running frame
  sequencer to stay audible), timer=253 (expected freq
  1789773 / (16 * 254) ~= 440.36Hz), enables it via $4015 (VGM reg 0x15),
  waits ~0.4s, ends. This is the second (after `ym2151-tone.vgm`) of the
  second wave of chips added in VgmEngine.cs (the 25-chip batch: RF5C68/
  RF5C164/PWM/C140/OKIM6258/OKIM6295/Y8950/YMF278B/YMF271/YMZ280B/DMG/NES+
  DMC+FDS/MultiPCM/uPD7759/K054539/HuC6280/K053260/POKEY/QSound/WSwan/
  SAA1099/ES5503/X1_010/C352/GA20), chosen because the user explicitly named
  NES APU (and OKIM6258) by name when asking for full chip coverage. Verified
  via Python WAV analysis: not silent/clipped (max amplitude 2490/32767, all
  18432 samples nonzero), and the measured duty-cycle transition period
  works out to ~440.38Hz - matching the 440.36Hz expected frequency almost
  exactly. OKIM6258 itself was not given a dedicated fixture because,
  unlike a plain register-write chip, actually hearing it requires encoding
  an ADPCM data block (VGM command 0x67) as well as stream-control commands,
  not just a handful of register writes - a meaningfully bigger lift than
  every other fixture here. The remaining 24 chips of this batch share the
  same simple single-instance wiring pattern as the rest and weren't each
  given a dedicated fixture either (see macos/README.md for the full
  rationale).

  Building this fixture also surfaced a real gotcha in `Driver/vgm.cs`: its
  EOF check uses the header's 0x04 field ("EOF offset") as an absolute
  buffer offset *directly*, without adding the VGM-spec-mandated +4 (spec:
  the field is relative to byte 4, so true EOF = field + 4; this code just
  uses `field` as-is). Encoding the field the spec-correct way
  (`filelen - 4`) made the engine's EOF check fire exactly 4 bytes early,
  silently truncating this fixture's trailing wait+end commands (caught via
  a temporary debug build that traced `vgmAdr`/`vgmWait` - the earlier
  fixtures happen to not hit this because their wait command starts safely
  before that 4-byte window). Worked around here by encoding the field as
  the literal total file length instead. Real-world `.vgm`/`.vgz` files
  encode this field the spec-correct way, so this is worth keeping in mind
  once real VGM files are tested (see macos/README.md's "다음 단계 후보").

- `s98-sn76489-tone.s98` (60 bytes) - a hand-built S98 v3 file (the format
  `MusicEngine.LoadS98` reads: 0x20-byte header + one 0x10-byte device table
  entry declaring SN76489 at clock 3579545 + a tiny register-dump command
  stream). Sets the tone frequency (timer value 254, ~440.4Hz) then the
  channel-0 volume to max via two SN76489 data-byte writes (S98's per-device
  command format only uses the second payload byte for this chip, mirroring
  VGM's own single-byte SN76489 write), then a single `0xFE` "wait N syncs"
  command (N=40, ~0.4s at the default 10ms/sync rate) before the `0xFD` end
  command (no loop address set, so playback stops there). Verified via Python
  WAV analysis: 18431/18432 nonzero samples, max amplitude 2048/32767, and a
  duty-cycle transition period working out to ~440.40Hz - almost exactly the
  440Hz target. This is the first (and, this round, only) S98 fixture -
  chosen because S98 is architecturally closest to VGM (a per-file dynamic
  chip list read from a register-dump command stream) among the 6 newly
  ported formats, so it's the cheapest to hand-encode and gives the most
  direct confidence in `MusicEngine.LoadS98`'s dynamic per-`DeviceType` chip
  wiring.

- `nsf-apu-tone.nsf` (155 bytes) - a hand-built minimal NSF file: a full
  0x80-byte NSF 1.0 header (load/init/play addresses 0x8000/0x8000/0x801A,
  1 song, NTSC-only, no expansion audio chips) followed by real 6502 machine
  code - unlike every other fixture here, NSF playback runs actual CPU
  instructions rather than a register-write command stream. The `init`
  routine (at 0x8000) writes the NES APU's pulse channel 1 registers
  directly ($4015=$01 enable, $4001=$00 disable sweep, $4000=$BF duty
  10%/halt/constant volume 15, $4002/$4003=timer 253 low/high, expected freq
  1789773 / (16 * 254) ~= 440.19Hz) and returns; the `play` routine (at
  0x801A) is just `RTS` since the tone is already latched and doesn't need
  per-frame updates. This exercises the `MusicEngine.LoadNsf` →
  `nsf.Render()` custom-PCM path end-to-end,
  including the CPU actually executing the init routine's ten instructions
  in the right order with the right operands (confirmed via `km6502.cs`'s
  built-in `#if TRACE` CPU trace, which Release builds enable by default -
  harmless but noisy if you run this fixture directly and watch stdout).
  Verified via Python WAV analysis: all 20000 sampled frames nonzero, and a
  duty-cycle transition period working out to ~440.47Hz. Building this
  fixture also surfaced an unrelated but real bug: `nsf.cs`'s
  `getGD3Info()` unconditionally calls `Encoding.GetEncoding(932)` to decode
  the title/artist tags, and this port's Shift-JIS codepage registration was
  dead code (see macos/README.md) - any NSF (or S98/MXDRV/MNDRV) file would
  have thrown `NotSupportedException` before this was fixed.

Run any of these with:

```
cd macos/EngineSmokeTest
dotnet run -c Release -- testdata/sn76489-tone.vgm /tmp/out.wav
dotnet run -c Release -- testdata/ym2612-fm-tone.vgm /tmp/out-fm.wav
dotnet run -c Release -- testdata/ym2151-tone.vgm /tmp/out-opm.wav
dotnet run -c Release -- testdata/sn76489-tone.vgz /tmp/out-vgz.wav
dotnet run -c Release -- testdata/nes-apu-tone.vgm /tmp/out-nes.wav
dotnet run -c Release -- testdata/s98-sn76489-tone.s98 /tmp/out-s98.wav
dotnet run -c Release -- testdata/nsf-apu-tone.nsf /tmp/out-nsf.wav
```

Note: SID/NSF tunes have no defined end (they can loop or idle forever), so
`nsf-apu-tone.nsf` will run until `EngineSmokeTest`'s ~60s safety cap rather
than stopping on its own - this is expected, not a hang.
