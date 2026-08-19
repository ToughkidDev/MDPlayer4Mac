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

Run any of these with:

```
cd macos/EngineSmokeTest
dotnet run -c Release -- testdata/sn76489-tone.vgm /tmp/out.wav
dotnet run -c Release -- testdata/ym2612-fm-tone.vgm /tmp/out-fm.wav
dotnet run -c Release -- testdata/ym2151-tone.vgm /tmp/out-opm.wav
```
