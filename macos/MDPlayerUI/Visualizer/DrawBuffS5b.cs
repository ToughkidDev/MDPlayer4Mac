// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmS5B.cs's screenDrawParams path calls: Volume, KeyBoard (the generic single-arg-y
// variant, not KeyBoardDCSG - see below), ToneNoise, ChS5B, Nfrq, Efrq, Etype, plus the
// shared drawFont8/drawFont4/drawFont4Int/drawFont4Int2/drawKbn primitives those build on.
// S5B (Sunsoft FME-7/5B, the AY-3-8910-compatible expansion sound chip used by some NES
// Famicom cartridges/mappers) has the same 3 tone/noise channels + hardware envelope
// generator as AY8910, but frmS5B.cs's window is simpler still: no per-channel decimal
// volume readout, no tone-period hex readout, no envelope-mode flag icon - just volume bar,
// keyboard, tone/noise mode icon, and channel badge per row.
//
// KeyBoard vs KeyBoardDCSG: frmS5B.cs calls drawBuff.cs's generic `KeyBoard(screen, y, ref
// ot, nt, tp)` (drawBuff.cs:1315), not the `KeyBoardDCSG(screen, x, y, ref ot, nt)` that
// SN76489/AY8910 use. The generic one takes a channel index (not a pixel x) and has its
// note-name-text x-offset hardcoded to 296/312 (vs. KeyBoardDCSG's 288+x/304+x, parameterized
// by whatever x the caller passes) - genuinely different coordinates, ported separately
// rather than reusing DrawBuffAy8910's.
//
// Same deliberate simplifications as every other DrawBuffXxx.cs in this port (tp hardcoded
// to 0; mask kept in signatures but never set true). Self-contained per this port's
// established per-chip-file convention.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffS5b
    {
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4 / digit glyphs)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RPsgEnv = null!; // rPSGEnv (hw envelope shape icon)
        public static SpriteAtlas RPsgMode0 = null!; // rPSGMode_01 (tone/noise icon, tp=0/unmasked)
        public static SpriteAtlas RPsgMode1 = null!; // rPSGMode_02 (tone/noise icon, tp=0/masked)

        public static void LoadSprites()
        {
            RVol = SpriteAtlas.Load("rVol_01");
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RPsgEnv = SpriteAtlas.Load("rPSGEnv");
            RPsgMode0 = SpriteAtlas.Load("rPSGMode_01");
            RPsgMode1 = SpriteAtlas.Load("rPSGMode_02");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - S5B only ever calls this with c=0 (mono).
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

        // drawBuff.cs:3638 drawKbn.
        public static void DrawKbn(PixelScreen screen, int x, int y, int t)
        {
            switch (t)
            {
                case 0: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 0, 0, 4, 8); break;
                case 1: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 4, 0, 3, 8); break;
                case 2: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 8, 0, 4, 8); break;
                case 3: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 12, 0, 4, 8); break;
                case 4: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 0 + 16, 0, 4, 8); break;
                case 5: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 4 + 16, 0, 3, 8); break;
                case 6: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 8 + 16, 0, 4, 8); break;
                case 7: screen.DrawIntArray(x, y, RKbd.Pixels, 32, 12 + 16, 0, 4, 8); break;
            }
        }

        // drawBuff.cs:3680 drawFont8. t: 0=unmasked colour, 1=masked colour.
        public static void DrawFont8(PixelScreen screen, int x, int y, int t, string msg)
        {
            SpriteAtlas src = t == 0 ? RFont1_0 : RFont1_1;
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, src.Pixels, 128, (cd % 16) * 8, (cd / 16) * 8, 8, 8);
                x += 8;
            }
        }

        // drawBuff.cs:3789 drawFont4 (text glyphs; t always 0, see file header).
        public static void DrawFont4(PixelScreen screen, int x, int y, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3801 drawFont4Int - leading-zero-blanked numeric glyphs (Nfrq, k=2).
        public static void DrawFont4Int(PixelScreen screen, int x, int y, int k, int num)
        {
            int n = num / 10;
            num -= n * 10;
            n = n > 9 ? 0 : n;
            if (n != 0)
            {
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
            }
            else
            {
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, 0, 0, 4, 8);
            }

            n = num / 1;
            x += 4;
            screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
        }

        // drawBuff.cs:3937 drawFont4Int2 - never blanks the leading digit (Etype).
        private static void DrawFont4Int2(PixelScreen screen, int x, int y, int num)
        {
            int n = num / 10;
            num -= n * 10;
            n = n > 9 ? 0 : n;
            screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);

            n = num / 1;
            x += 4;
            screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
        }

        // drawBuff.cs:1315 KeyBoard - piano-key highlight + note-name/octave readout, the
        // generic (non-DCSG) variant with x hardcoded to 32 and text at 296/312 (see file
        // header for why this differs from KeyBoardDCSG). `c` is the channel index.
        public static void KeyBoard(PixelScreen screen, int c, ref int ot, int nt)
        {
            if (ot == nt) return;

            int y = (c + 1) * 8;

            if (ot >= 0 && ot < 12 * 8)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, 32 + kx, y, kt);
            }

            if (nt >= 0 && nt < 12 * 8)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, 32 + kx, y, kt);
            }

            DrawFont8(screen, 296, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 296, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 312, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:3674 ToneNoiseP - one 8x8 tone/noise-mode icon tile.
        private static void ToneNoiseP(PixelScreen screen, int x, int y, int t, int ntp)
        {
            SpriteAtlas src = ntp == 0 ? RPsgMode0 : RPsgMode1;
            screen.DrawIntArray(x, y, src.Pixels, 32, 8 * t, 0, 8, 8);
        }

        // drawBuff.cs:2583 ToneNoise - dirty-diff wrapper. frmS5B.cs always passes tp=0
        // (no mask-state bit folded in, unlike AY8910's caller) since S5B's mute-toggle UI
        // was never wired up in the original either.
        public static void ToneNoise(PixelScreen screen, int x, int y, int c, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;

            ToneNoiseP(screen, x * 4, y * 4 + c * 8, nt, ntp);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4374 ChS5B_P - channel-number badge (same coordinates as ChAY8910_P).
        private static void ChS5bP(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 32, 0, 16, 8);
            DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:2006 ChS5B - dirty-diff wrapper.
        public static void ChS5b(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChS5bP(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:2596 Nfrq - chip-wide hardware-envelope Frequency-divider readout.
        public static void Nfrq(PixelScreen screen, int x, int y, ref int onfrq, int nnfrq)
        {
            if (onfrq == nnfrq) return;
            DrawFont4Int(screen, x * 4, y * 4, 2, nnfrq);
            onfrq = nnfrq;
        }

        // drawBuff.cs:2610 Efrq - chip-wide hardware-envelope Frequency (16-bit) readout.
        public static void Efrq(PixelScreen screen, int x, int y, ref int oefrq, int nefrq)
        {
            if (oefrq == nefrq) return;
            DrawFont4(screen, x * 4, y * 4, nefrq.ToString("D5"));
            oefrq = nefrq;
        }

        // drawBuff.cs:2624/4262 Etype/drawEtypeP - chip-wide hardware-envelope Type readout.
        public static void Etype(PixelScreen screen, int x, int y, ref int oetype, int netype)
        {
            if (oetype == netype) return;

            int px = x * 4;
            int py = y * 4;
            screen.DrawIntArray(px, py, RPsgEnv.Pixels, 128, 8 * netype, 0, 8, 8);
            DrawFont4Int2(screen, px + 12, py, netype);

            oetype = netype;
        }
    }
}
