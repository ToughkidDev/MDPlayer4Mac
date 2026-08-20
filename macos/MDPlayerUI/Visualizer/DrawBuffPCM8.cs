// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmPCM8.cs's screenDrawParams path calls. PCM8 is the X68000's "PCM8" 8-voice sample-driver
// convention (used by the MXDRV/ZMS/RCS sequenced-music formats, similar in spirit to
// MpcmX68k). 16 channel rows are drawn (matching MDChipParams.PCM8's Channel[16]), each
// showing a channel badge, a raw single-tile pan icon, 32-bit sample pointer/length hex
// readouts, 2-digit decimal readouts for PCM mode and playback rate, a 2-digit decimal raw
// volume-register readout, and a volume LED bar - though only the first 8 rows are ever
// actually driven by live data (see PCM8Visualizer.cs's header for why).
//
// Data source: same architectural shape as MpcmX68k - reads driver-internal state directly
// (this port's live driver, not the dead AudioShim.Audio.DriverVirtual stub - see
// PCM8Visualizer.cs).
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffPCM8
    {
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Hex*/Int2, t=0)
        public static SpriteAtlas RFont2_1 = null!; // rFont_04 (drawFont4Int2, masked, t=1 - for the badge digits)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan = null!; // rPan_01 (raw single-tile stereo pan indicator)

        public static void LoadSprites()
        {
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RFont2_1 = SpriteAtlas.Load("rFont_04");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
            RPan = SpriteAtlas.Load("rPan_01");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y, called with c=0 here (single column, no
        // L/R split).
        public static void Volume(PixelScreen screen, int x, int y, int c, ref int ov, int nv)
        {
            if (ov == nv) return;

            int t = 0;
            int sy = 0;
            if (c == 1 || c == 2) { t = 4; }
            if (c == 2) { sy = 4; }

            for (int i = 0; i <= 19; i++)
            {
                VolumeP(screen, x + i * 2, y + sy, 1 + t);
            }

            for (int i = 0; i <= nv; i++)
            {
                VolumeP(screen, x + i * 2, y + sy, i > 17 ? 2 + t : 0 + t);
            }

            ov = nv;
        }

        // drawBuff.cs:3789 drawFont4.
        private static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            SpriteAtlas src = t == 0 ? RFont2_0 : RFont2_1;
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, src.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:4195 drawFont4Hex32Bit / :3609 font4Hex32Bit - same shape already ported
        // independently in DrawBuffMpcmX68k.cs/DrawBuffOKIM6295.cs; duplicated again here.
        private static void DrawFont4Hex32Bit(PixelScreen screen, int x, int y, uint num)
        {
            num = Common.Range(num, 0, 0xffff_ffff);

            uint n = num / 0x1000_0000;
            num -= n * 0x1000_0000;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x100_0000;
            num -= n * 0x100_0000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x10_0000;
            num -= n * 0x10_0000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x1_0000;
            num -= n * 0x1_0000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x1000;
            num -= n * 0x1000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x100;
            num -= n * 0x100;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x10;
            num -= n * 0x10;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 1;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);
        }

        public static void Font4Hex32Bit(PixelScreen screen, int x, int y, ref uint on, uint nn)
        {
            if (on == nn) return;
            DrawFont4Hex32Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3937 drawFont4Int2 (zero-padded 2-digit) - this chip only ever passes
        // k=2, so (like DrawBuffYm2610.cs's version) the k==3 branch is omitted rather than
        // ported as dead code.
        private static void DrawFont4Int2(PixelScreen screen, int x, int y, int t, int num)
        {
            int n = num / 10;
            num -= n * 10;
            n = n > 9 ? 0 : n;
            screen.DrawIntArray(x, y, (t == 0 ? RFont2_0 : RFont2_1).Pixels, 128, n * 4 + 64, 0, 4, 8);

            n = num / 1;
            x += 4;
            screen.DrawIntArray(x, y, (t == 0 ? RFont2_0 : RFont2_1).Pixels, 128, n * 4 + 64, 0, 4, 8);
        }

        public static void Font4Int2(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int2(screen, x, y, 0, nn);
            on = nn;
        }

        // drawBuff.cs:4269 drawPanP / :1855 Pan - raw single-tile pan indicator (the same
        // rPan_01 sprite already used by DrawBuffSn76489.cs/DrawBuffDmg.cs/
        // DrawBuffMpcmX68k.cs/DrawBuffOKIM6258.cs), duplicated again here.
        private static void DrawPanP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPan.Pixels, 32, 8 * t, 0, 8, 8);
        }

        public static void Pan(PixelScreen screen, int x, int y, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;
            DrawPanP(screen, x, y, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4445 ChPCM8_P - wide 24px badge with drawFont4Int2 for the channel
        // number (up to 16 channels, matching ChMPCMX68k_P's shape).
        private static void ChPCM8P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 64, 0, 24, 8);
            DrawFont4Int2(screen, x + 24, y, mask ? 1 : 0, 1 + ch);
        }

        // drawBuff.cs:2108 ChPCM8 - dirty-diff wrapper.
        public static void ChPCM8(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChPCM8P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
