# Visualizer sprite assets

Raw `.rgba32` pixel dumps of the original Windows MDPlayer's chip-visualizer sprite
sheets (`MDPlayer/MDPlayerx64/Resources/*.png`), re-exported via
`macos/tools/export_sprites.py` rather than shipped as PNGs - see
`macos/MDPlayerUI/Visualizer/SpriteAtlas.cs`'s header comment for why (short version:
avoids depending on Avalonia's own PNG decoder, which this sandbox has never been able
to build-check).

Format: 4-byte width (int32 LE), 4-byte height (int32 LE), then `width*height` int32 LE
pixels in row-major order, each a standard `0xAARRGGBB` value.

## Currently exported (SN76489 visualizer only - see macos/README.md's chip-visualizer
section for the full plan)

| File | Source PNG | Used by |
|---|---|---|
| `planeSN76489.rgba32` | `Resources/planeSN76489.png` | Static background (labels, table borders, default keyboard-grid/mode-text art) |
| `rVol_01.rgba32` | `Resources/rVol_01.png` | LED volume-bar meter tiles (`DrawBuffSn76489.Volume`) |
| `rKBD_01.rgba32` | `Resources/rKBD_01.png` | Piano-key shape tiles (`DrawBuffSn76489.DrawKbn`) |
| `rFont_01.rgba32` | `Resources/rFont_01.png` | 8px font, unmasked colour (`DrawBuffSn76489.DrawFont8`, t=0) |
| `rFont_02.rgba32` | `Resources/rFont_02.png` | 8px font, masked colour (`DrawBuffSn76489.DrawFont8`, t=1) |
| `rFont_03.rgba32` | `Resources/rFont_03.png` | 4px font (`DrawBuffSn76489.DrawFont4`) |
| `rType_01.rgba32` | `Resources/rType_01.png` | Channel-number badge, unmasked (`DrawBuffSn76489.ChSN76489P`) |
| `rType_02.rgba32` | `Resources/rType_02.png` | Channel-number badge, masked |
| `rPan_01.rgba32` | `Resources/rPan_01.png` | Pan indicator (`DrawBuffSn76489.DrawPanP`) |

Only the `tp=0` (software-emulated chip) variant of each multi-variant sprite sheet was
exported - this port's engine never supports real-hardware output (`tp=1`/`tp=2`), so
those variants would never be selected. See `DrawBuffSn76489.cs`'s header comment.

## Adding more (e.g. for the planned YM2612 visualizer)

1. Find which sprite sheets the target `frmXxxx.cs`/its `DrawBuff.screenInitXxxx`
   actually reference (grep `ResMng.ImgDic["..."]` in the relevant Windows source).
2. `python3 macos/tools/export_sprites.py <source PNGs...> macos/MDPlayerUI/Assets/Visualizer/`
3. Port the drawing functions (a new `DrawBuffYm2612.cs` alongside `DrawBuffSn76489.cs`,
   following the same pattern) and the chip window logic (a new `Ym2612Visualizer.cs`
   alongside `Sn76489Visualizer.cs`).
