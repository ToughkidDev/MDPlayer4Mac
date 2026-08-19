# testdata

Two hand-built, minimal VGM files used to verify `EngineSmokeTest` actually
produces correct audio (not just that the build compiles). Both are valid
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

Run either with:

```
cd macos/EngineSmokeTest
dotnet run -c Release -- testdata/sn76489-tone.vgm /tmp/out.wav
dotnet run -c Release -- testdata/ym2612-fm-tone.vgm /tmp/out-fm.wav
```
