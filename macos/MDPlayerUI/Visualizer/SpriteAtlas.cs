// Loads the small sprite-sheet images the chip channel visualizers blit onto their screen
// (LED volume bars, piano-key shapes, bitmap fonts, channel-number badges, pan indicators -
// see macos/MDPlayerUI/Assets/Visualizer/README.md for the full asset list and where each
// one comes from). These started life as the original Windows MDPlayer's
// MDPlayer/MDPlayerx64/Resources/*.png files (loaded there via GDI+ Bitmap.LockBits into a
// raw int[] - see drawBuff.cs's getIntArray) - re-exported here as a tiny custom ".rgba32"
// binary format (4-byte width, 4-byte height, then width*height little-endian int32 ARGB
// pixels, one per source PNG pixel) rather than shipping the PNGs themselves and decoding
// them at runtime.
//
// Why not just decode the PNGs directly with Avalonia's own Bitmap/Skia decoder at
// runtime? Because this whole project (see MDPlayerUI.csproj's header comment) was authored
// in a Linux sandbox with no nuget.org access, so NONE of its Avalonia-dependent code has
// ever been locally build-checked, let alone run - it only gets verified when the user
// builds it for real on their Mac. Avalonia's Bitmap.CopyPixels (or equivalent pixel-read
// API) is very likely fine, but it's one more surface this port has never actually
// exercised. A hand-rolled binary format read with plain BinaryReader.ReadInt32() calls has
// no such uncertainty - every .NET runtime on every platform reads little-endian int32s the
// same way - so it removes a whole category of "does this Avalonia API even work the way I
// think it does" risk from a change that's already accumulating a lot of untestable surface
// area. See tools/export_sprites.py (documented in the Assets/Visualizer README) for how to
// regenerate/add more .rgba32 files from the original Windows PNGs.
using System;
using System.IO;
using Avalonia.Platform;

namespace MDPlayer.UI.Visualizer
{
    // One decoded sprite sheet: raw ARGB pixels (standard 0xAARRGGBB int32, matching
    // exactly what the original drawBuff.cs's getIntArray/FrameBuffer.drawIntArray work
    // with) plus its width, so callers can index into it the same way the original code
    // indexes its int[] sprite arrays (row-major, stride == width).
    public sealed class SpriteAtlas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int[] Pixels;

        private SpriteAtlas(int width, int height, int[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        // assetName is relative to Assets and omits the extension, e.g.
        // "Visualizer/planeSN76489" or "Transport/ccPlay".
        public static SpriteAtlas Load(string assetName)
        {
            string relativeName = assetName.Contains('/') ? assetName : $"Visualizer/{assetName}";
            Uri uri = new($"avares://MDPlayer4Mac/Assets/{relativeName}.rgba32");
            using Stream stream = AssetLoader.Open(uri);
            using BinaryReader reader = new(stream);

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            int[] pixels = new int[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = reader.ReadInt32();
            }

            return new SpriteAtlas(width, height, pixels);
        }
    }
}
