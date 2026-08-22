// Dashboard counter drawn with the exact 8x8 bitmap digit sheet used by Windows
// drawBuff.drawTimer / drawFont8Int2 (rFont_01).  Keeping this separate from the
// platform text renderer preserves the original pixel-art timing readout at every scale.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class DashboardTimerDisplay
    {
        private const int TimerWidth = 64;
        private const int TimerHeight = 8;
        private readonly SpriteAtlas font = SpriteAtlas.Load("rFont_01");
        private readonly int foreground;

        public PixelScreen Screen { get; } = new();

        public DashboardTimerDisplay()
        {
            Screen.Init(TimerWidth * 3, TimerHeight, zoom: 1);
            Screen.IsHitTestVisible = false;
            foreground = FindForegroundColour();
            Update(0, 0, -1);
        }

        public void SetDisplayScale(double scale) => Screen.SetDisplayScale(scale);

        public void Update(long current, long total, long loopPosition)
        {
            Array.Fill(Screen.Buffer, unchecked((int)0xff000000));
            DrawClock(0, current, available: true);
            DrawClock(TimerWidth, total, available: total > 0);
            DrawClock(TimerWidth * 2, loopPosition, available: loopPosition >= 0);
            Screen.Present();
        }

        private void DrawClock(int x, long sampleCounter, bool available)
        {
            if (!available)
            {
                for (int digit = 0; digit < 7; digit++)
                    DrawDash(x + (digit < 3 ? digit * 8 : digit < 5 ? 4 + digit * 8 : 8 + digit * 8));
                DrawSeparator(x + 24, colon: true);
                DrawSeparator(x + 44, colon: false);
                return;
            }

            // frmMain's counters use VGM sample ticks.  Use integer arithmetic so the
            // hundredth does not flicker at a floating-point rounding boundary.
            long hundredthsTotal = Math.Max(0, sampleCounter) * 100 / Common.VGMProcSampleRate;
            long secondsTotal = hundredthsTotal / 100;
            int hundredths = (int)(hundredthsTotal % 100);
            int seconds = (int)(secondsTotal % 60);
            int minutes = (int)Math.Min(999, secondsTotal / 60);

            // This follows drawFont8Int2's three-digit minute field: the first zero is
            // blank, while the final two minute digits remain visible.
            DrawDigit(x, minutes / 100, blank: minutes < 100);
            DrawDigit(x + 8, (minutes / 10) % 10);
            DrawDigit(x + 16, minutes % 10);
            DrawSeparator(x + 24, colon: true);
            DrawDigit(x + 28, seconds / 10);
            DrawDigit(x + 36, seconds % 10);
            DrawSeparator(x + 44, colon: false);
            DrawDigit(x + 48, hundredths / 10);
            DrawDigit(x + 56, hundredths % 10);
        }

        private void DrawDigit(int x, int digit, bool blank = false)
        {
            // rFont_01 row 1 contains 0..9 at 8px intervals; its first tile is also
            // the blank cell selected by drawFont8Int2 for a suppressed leading digit.
            Screen.DrawIntArray(x, 0, font.Pixels, font.Width, blank ? 0 : digit * 8,
                blank ? 0 : 8, 8, 8);
        }

        private void DrawDash(int x)
        {
            for (int offset = 2; offset < 6; offset++) SetPixel(x + offset, 4);
        }

        private void DrawSeparator(int x, bool colon)
        {
            SetPixel(x + 1, colon ? 2 : 6);
            SetPixel(x + 2, colon ? 2 : 6);
            if (colon)
            {
                SetPixel(x + 1, 6);
                SetPixel(x + 2, 6);
            }
        }

        private void SetPixel(int x, int y)
        {
            if (x >= 0 && x < Screen.NativeWidth && y >= 0 && y < Screen.NativeHeight)
                Screen.Buffer[y * Screen.NativeWidth + x] = foreground;
        }

        private int FindForegroundColour()
        {
            // The sprite's black background is opaque. Select a real glyph pixel so the
            // two hand-drawn separators use the exact same colour as the Windows digits.
            for (int y = 8; y < Math.Min(16, font.Height); y++)
            for (int x = 0; x < Math.Min(80, font.Width); x++)
            {
                int pixel = font.Pixels[y * font.Width + x];
                if ((pixel & 0x00ffffff) != 0) return pixel;
            }

            return unchecked((int)0xffaab6ff);
        }
    }
}
