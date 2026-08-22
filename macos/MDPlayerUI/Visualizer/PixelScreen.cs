// Avalonia replacement for the original Windows MDPlayer's FrameBuffer.cs/DoubleBuffer.cs
// pair (MDPlayer/MDPlayerx64/FrameBuffer.cs, DoubleBuffer.cs) - those existed purely to work
// around GDI+/WinForms not natively supporting fast raw-pixel-buffer blitting with built-in
// double buffering (see the header comment on macos/MDPlayerUI/Assets/Visualizer/README.md
// for the fuller research writeup). Avalonia doesn't need any of that: it already
// double-buffers via its own compositor, and WriteableBitmap gives the same "lock, write
// raw pixels, unlock" access GDI+'s BitmapData.Scan0 did.
//
// This control owns ONE persistent int[] ARGB pixel buffer (Buffer, row-major,
// stride == Width - same layout the original's baPlaneBuffer used) and a same-sized
// WriteableBitmap it gets copied into on Present(). Unlike the original, this does NOT keep
// a separate "static background" layer underneath a transparent sprite overlay: every chip
// visualizer function this port actually uses (see DrawBuffSn76489.cs) draws fully opaque
// sprites via drawIntArray, which never reads or writes an alpha channel - so the simplest
// correct model is "draw the static background once into Buffer, then let every subsequent
// frame's sprite blits overwrite pixels directly on top of it", with no compositing step
// needed at all. (The original's drawByteArrayTransp, which DOES honor a magic-green
// colorkey for irregular sprite shapes, isn't used by anything this port has ported yet -
// see the header comment on DrawBuffSn76489.cs.)
using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MDPlayer.UI.Visualizer
{
    public sealed class PixelScreen : Avalonia.Controls.Control
    {
        public int NativeWidth { get; private set; }
        public int NativeHeight { get; private set; }

        // Row-major, stride == NativeWidth, standard 0xAARRGGBB int32 per pixel - exactly
        // the same layout/format the original's FrameBuffer.baPlaneBuffer used, so every
        // ported drawXxx helper (DrawBuffSn76489.cs) can be a near-literal transcription of
        // the original drawBuff.cs functions.
        public int[] Buffer = Array.Empty<int>();

        private WriteableBitmap? bitmap;

        public void Init(int nativeWidth, int nativeHeight, int zoom)
        {
            NativeWidth = nativeWidth;
            NativeHeight = nativeHeight;
            Buffer = new int[nativeWidth * nativeHeight];
            bitmap = new WriteableBitmap(
                new PixelSize(nativeWidth, nativeHeight),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            Width = nativeWidth * zoom;
            Height = nativeHeight * zoom;
        }

        // Changes only the Avalonia display rectangle. The backing bitmap and native pixel
        // buffer remain untouched, so channel visualizers can switch cleanly between the
        // Windows-style 2x view and the compact 1x view without reinitializing state.
        public void SetDisplayScale(double scale)
        {
            if (NativeWidth <= 0 || NativeHeight <= 0 || scale <= 0) return;
            Width = NativeWidth * scale;
            Height = NativeHeight * scale;
            InvalidateMeasure();
        }

        // Direct port of FrameBuffer.drawIntArray (MDPlayer/MDPlayerx64/FrameBuffer.cs) -
        // unconditional pixel copy (no transparency/colorkey check), used by every sprite
        // blit this port needs. src/srcWidth is the source sprite sheet (a SpriteAtlas's
        // Pixels/Width); imgX/imgY/imgWidth/imgHeight select the sub-rectangle within it;
        // x/y is the destination position in this screen's own Buffer.
        public void DrawIntArray(int x, int y, int[] src, int srcWidth, int imgX, int imgY, int imgWidth, int imgHeight)
        {
            if (Buffer.Length == 0) return;

            int adr1 = NativeWidth * y + x;
            int adr2 = srcWidth * imgY + imgX;

            for (int i = 0; i < imgHeight; i++)
            {
                if (adr1 >= 0 && adr2 >= 0)
                {
                    for (int j = 0; j < imgWidth; j++)
                    {
                        if (adr1 + j >= Buffer.Length) continue;
                        if (adr2 + j >= src.Length) continue;

                        Buffer[adr1 + j] = src[adr2 + j];
                    }
                }

                adr1 += NativeWidth;
                adr2 += srcWidth;
            }
        }

        // Pushes Buffer into the backing WriteableBitmap and asks Avalonia to repaint.
        // Call this once per redraw tick after a batch of DrawIntArray calls, not per call -
        // mirrors the original's screenMainLoop pattern (decode+draw off-thread, one
        // Refresh()/update() per tick - see frmMain.cs:5262-5276) of batching all of a
        // frame's changes before pushing to screen.
        public unsafe void Present()
        {
            if (bitmap == null || Buffer.Length == 0) return;

            using ILockedFramebuffer fb = bitmap.Lock();
            // PixelFormat.Bgra8888 stores each pixel as bytes [B,G,R,A] in memory, which is
            // exactly how a little-endian int32 0xAARRGGBB is laid out in memory - so each
            // row is a straight bulk copy, not a per-pixel channel shuffle. Copied row by
            // row (rather than one flat copy of the whole buffer) because fb.RowBytes can
            // be larger than NativeWidth*4 if the backing surface pads each row for
            // alignment - a flat copy would silently shear the image sideways whenever that
            // padding is nonzero.
            int rowBytes = NativeWidth * 4;
            fixed (int* src = Buffer)
            {
                byte* srcBytes = (byte*)src;
                byte* dst = (byte*)fb.Address;
                for (int y = 0; y < NativeHeight; y++)
                {
                    System.Buffer.MemoryCopy(srcBytes + y * rowBytes, dst + y * fb.RowBytes, fb.RowBytes, rowBytes);
                }
            }

            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (bitmap == null) return;

            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            {
                context.DrawImage(bitmap, new Rect(0, 0, NativeWidth, NativeHeight), new Rect(Bounds.Size));
            }
        }
    }
}
