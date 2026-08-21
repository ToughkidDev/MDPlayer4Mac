# Visualizer sprite assets

Raw `.rgba32` pixel dumps of the original Windows MDPlayer's chip-visualizer sprite
sheets (`MDPlayer/MDPlayerx64/Resources/*.png`), re-exported via
`macos/tools/export_sprites.py` rather than shipped as PNGs - see
`macos/MDPlayerUI/Visualizer/SpriteAtlas.cs`'s header comment for why (short version:
avoids depending on Avalonia's own PNG decoder, which this sandbox has never been able
to build-check).

Format: 4-byte width (int32 LE), 4-byte height (int32 LE), then `width*height` int32 LE
pixels in row-major order, each a standard `0xAARRGGBB` value.

## Currently exported (channel visualizers + pixel mixer)

| File | Source PNG | Used by |
|---|---|---|
| `planeSN76489.rgba32` | `Resources/planeSN76489.png` | SN76489 static background (labels, table borders, default keyboard-grid/mode-text art) |
| `planeYM2612.rgba32` | `Resources/planeYM2612.png` | YM2612 static background (labels, table borders, instrument-table grid, default keyboard/mode-text art) |
| `rVol_01.rgba32` | `Resources/rVol_01.png` | LED volume-bar meter tiles (`Volume`, both chips) |
| `rKBD_01.rgba32` | `Resources/rKBD_01.png` | Piano-key shape tiles (`DrawKbn`, both chips) |
| `rFont_01.rgba32` | `Resources/rFont_01.png` | 8px font, unmasked colour (`DrawFont8`, t=0, both chips) |
| `rFont_02.rgba32` | `Resources/rFont_02.png` | 8px font, masked colour (`DrawFont8`, t=1, both chips) |
| `rFont_03.rgba32` | `Resources/rFont_03.png` | 4px font + numeric digit glyphs (`DrawFont4`/`DrawFont4Int`, both chips) |
| `rType_01.rgba32` | `Resources/rType_01.png` | Channel-number/type badge, unmasked (both chips) |
| `rType_02.rgba32` | `Resources/rType_02.png` | Channel-number/type badge, masked |
| `rPan_01.rgba32` | `Resources/rPan_01.png` | Pan indicator (`DrawPanP`, both chips) |
| `rNESDMC.rgba32` | `Resources/rNESDMC.png` | Operator on/off "slot" icons (`DrawBuffYm2612.Slot`) - despite the name, this sprite sheet is shared with the (unported) NES DMC visualizer in the original app, not YM2612-specific art |
| `rFader.rgba32` | `Resources/rFader.png` | Windows `frmMixer2` fader rail, master/chip knobs, and 2px level bar tiles (`MixerVisualizer`) |
| `planeMixer.rgba32` | `Resources/planeMixer.png` | Windows mixer layout and palette reference. The Mac mixer draws active slots dynamically instead of using this fixed all-chip background. |

`rVol_01`/`rKBD_01`/`rFont_01`/`rFont_02`/`rFont_03`/`rType_01`/`rType_02`/`rPan_01` are
genuinely the same shared sprite sheets in the original app, referenced by both
`DrawBuffSn76489.cs` and `DrawBuffYm2612.cs` - each file loads its own copies into its own
static fields rather than cross-referencing the other, so either chip's visualizer works
correctly even if a given VGM only drives one of them (see `DrawBuffYm2612.cs`'s header
comment for why).

Only the `tp=0` (software-emulated chip) variant of each multi-variant sprite sheet was
exported - this port's engine never supports real-hardware output (`tp=1`/`tp=2`), so
those variants would never be selected. See `DrawBuffSn76489.cs`/`DrawBuffYm2612.cs`'s
header comments.

## Adding more (e.g. for a future chip visualizer)

1. Find which sprite sheets the target `frmXxxx.cs`/its `DrawBuff.screenInitXxxx`
   actually reference (grep `ResMng.ImgDic["..."]` in the relevant Windows source).
2. `python3 macos/tools/export_sprites.py <source PNGs...> macos/MDPlayerUI/Assets/Visualizer/`
3. Port the drawing functions (a new `DrawBuffXxx.cs` alongside `DrawBuffSn76489.cs`/
   `DrawBuffYm2612.cs`, following the same pattern) and the chip window logic (a new
   `XxxVisualizer.cs` alongside `Sn76489Visualizer.cs`/`Ym2612Visualizer.cs`).
