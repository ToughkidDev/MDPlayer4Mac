// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmOKIM6258.cs's screenDrawParams path calls. OKIM6258 (OKI MSM6258, an ADPCM voice
// synthesis chip) is a single mono "channel" chip - just one 8px row - showing a raw
// single-tile pan icon, three 5-digit zero-padded decimal readouts (master clock frequency
// in kHz, clock divider, and derived playback frequency in kHz), independent L/R volume LED
// bars, and a static (no channel-number text) badge icon.
//
// Data source: reads chipRegister.GetOKIM6258Register(chipId) - a new one-line forward added
// to ChipRegister.cs alongside this file, forwarding to mds.ReadOKIM6258Status (mirroring
// Audio.GetOKIM6258Register - this port's ChipRegister keeps mds private, same reason a
// wrapper was needed for GetK053260Register/GetRf5c164Register before it).
//
// Genuinely-preserved original quirk: ChOKIM6258_P draws a fixed 24px-wide badge sprite with
// no appended channel-number text (unlike every multi-channel chip's Ch*_P, which appends a
// digit/pair via drawFont8/drawFont4Int2) - there's only one channel, so no number is needed.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port:
// screenInitOKIM6258's placeholder pre-draw loop is skipped (the first real ScreenDrawParams
// call naturally draws everything via the normal dirty-diff path).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffOKIM6258
    {
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan = null!; // rPan_01 (raw single-tile stereo pan indicator)

        public static void LoadSprites()
        {
            RFont2_0 = SpriteAtlas.Load("rFont_03");
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

        // drawBuff.cs:976 Volume - fixed x=256 row-index overload, same shape already ported
        // independently in DrawBuffMegaCD.cs; duplicated again here.
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

        // drawBuff.cs:3789 drawFont4 - this chip only ever passes t=0.
        public static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:4269 drawPanP - raw single-tile pan indicator (the same rPan_01 sprite
        // already used by DrawBuffSn76489.cs/DrawBuffDmg.cs/DrawBuffMpcmX68k.cs), duplicated
        // again here.
        private static void DrawPanP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPan.Pixels, 32, 8 * t, 0, 8, 8);
        }

        // drawBuff.cs:1940 PanToOKIM6258 - fixed x=24/y=8 overload (single channel, no
        // row index needed), tracked with a second dummy dirty-diff variable (otp/ntp)
        // mirroring the original's ref otp parameter.
        public static void PanToOKIM6258(PixelScreen screen, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;
            DrawPanP(screen, 24, 8, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4485 ChOKIM6258_P - fixed 24px badge, no channel-number text (only one
        // channel exists for this chip).
        private static void ChOKIM6258P(PixelScreen screen, int x, int y, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 8 * 8, 0, 24, 8);
        }

        // drawBuff.cs:2168 ChOKIM6258 - dirty-diff wrapper, fixed x=0/y=8 (single channel).
        public static void ChOKIM6258(PixelScreen screen, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChOKIM6258P(screen, 0, 8, nm ?? false);
            om = nm;
        }
    }
}
