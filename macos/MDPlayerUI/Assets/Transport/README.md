# Transport sprites

These are the original 16×16 Windows `frmMain` operation-button sprites, exported at
runtime-independent `.rgba32` form for `TransportSpriteButton`:

- `cc*`: normal
- `ch*`: hover
- `ci*`: active/pressed

The macOS player draws them with nearest-neighbour scaling at 2×, preserving the Windows
pixel-art transport layout. Regenerate with `macos/tools/export_sprites.py`, or use
`macos/tools/export_sprites.swift` on a Mac without Pillow.

`ccZoom`/`chZoom`/`ciZoom` are the Windows Zoom button sprites. In the macOS dashboard
they toggle every channel screen between its regular 2× presentation and compact 1×
presentation.
