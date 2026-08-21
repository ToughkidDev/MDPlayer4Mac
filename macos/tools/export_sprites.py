#!/usr/bin/env python3
"""Export original Windows MDPlayer sprite-sheet PNGs into the custom ".rgba32" raw
pixel format macos/MDPlayerUI/Visualizer/SpriteAtlas.cs reads at runtime.

Why not ship the PNGs and decode them at runtime? See SpriteAtlas.cs's header comment -
short version: this whole project has never been locally build-checked (no nuget.org
access in the sandbox it was authored in), so avoiding any reliance on Avalonia's own
PNG-decoding API removes one more unverifiable dependency from an already-unverified
change. A hand-rolled binary format needs nothing but BinaryReader.ReadInt32().

Format: 4-byte width (int32 LE), 4-byte height (int32 LE), then width*height int32 LE
pixels in row-major order, each a standard 0xAARRGGBB value (matching the in-memory
layout the original Windows drawBuff.cs/FrameBuffer.cs's int[] sprite arrays used, and
matching Avalonia's PixelFormat.Bgra8888 memory layout when read back as int32 - see
PixelScreen.cs).

Usage (run from the repo root, needs Pillow - `pip install pillow --break-system-packages`):
    python3 macos/tools/export_sprites.py \
        MDPlayer/MDPlayerx64/Resources/planeSN76489.png \
        macos/MDPlayerUI/Assets/Visualizer/

Pass any number of source PNGs; each becomes <name>.rgba32 in the output directory,
where <name> is the source file's name without its extension - this is also the name
SpriteAtlas.Load(name) expects.
"""
import struct
import sys
import os

def export_one(src_path, out_dir):
    from PIL import Image
    im = Image.open(src_path).convert("RGBA")
    w, h = im.size
    px = im.load()
    out = bytearray()
    out += struct.pack("<II", w, h)
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            argb = (a << 24) | (r << 16) | (g << 8) | b
            out += struct.pack("<i", argb if argb < 2**31 else argb - 2**32)

    name = os.path.splitext(os.path.basename(src_path))[0]
    out_path = os.path.join(out_dir, name + ".rgba32")
    with open(out_path, "wb") as f:
        f.write(out)
    print(f"{out_path}: {len(out)} bytes ({w}x{h})")

def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    *sources, out_dir = sys.argv[1:]
    os.makedirs(out_dir, exist_ok=True)
    for src in sources:
        export_one(src, out_dir)

if __name__ == "__main__":
    main()
