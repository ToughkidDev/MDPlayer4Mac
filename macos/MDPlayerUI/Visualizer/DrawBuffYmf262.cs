// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYMF262.cs's screenDrawParams path calls: SUSFlag, font4Int2, font4Hex12Bit, Pan,
// KeyBoard, VolumeXY, ChYMF262, drawNESSw, Kakko, plus the shared drawFont8/drawFont4/
// drawKbn primitives those build on. YMF262 (OPL3, the AdLib Gold/Sound Blaster 16's FM
// chip) has 18 FM channels across 2 register ports (9 channels each), with support for
// pairing adjacent channels into 4-operator mode (ConnectSelect, register 0x104) plus
// per-channel stereo pan - a real step up in complexity from YM3526/YM3812/Y8950's mono
// 2-operator-only channels - and the same 5 fixed-role rhythm channels as the other OPL
// chips.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0. Keeps DrawFont4's real masked/unmasked `t` parameter like the
// other OPL-family files, for the rhythm-row-label reason.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYmf262
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4, unmasked, t=0)
        public static SpriteAtlas RFont2_1 = null!; // rFont_04 (drawFont4, masked, t=1)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RNesDmc = null!;
        public static SpriteAtlas RPan = null!;
        public static SpriteAtlas RKakko = null!;

        public static void LoadSprites()
        {
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RFont2_1 = SpriteAtlas.Load("rFont_04");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
            RNesDmc = SpriteAtlas.Load("rNESDMC");
            RPan = SpriteAtlas.Load("rPan_01");
            RKakko = SpriteAtlas.Load("rKakko_00");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1171 VolumeXY - x/y are pre-scale-by-4 grid coordinates. Genuinely
        // keeps the `c` parameter here (0=Mono/1=Stereo(L)/2=Stereo(R) tile-colour select) -
        // unlike every other chip in this port, YMF262's screenDrawParams calls this with
        // c=1 for BOTH its L and R rows (they differ only by y, one grid row apart, not by
        // c) - transcribed exactly as the original does, not "fixed" to use c=2 for R.
        public static void VolumeXY(PixelScreen screen, int x, int y, int c, ref int ov, int nv)
        {
            if (ov == nv) return;

            int t = 0;
            int sy = 0;
            if (c == 1 || c == 2) { t = 4; }
            if (c == 2) { sy = 4; }

            y *= 4;
            x *= 4;

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

        // drawBuff.cs:3789 drawFont4 - real t parameter kept.
        public static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            SpriteAtlas src = t == 0 ? RFont2_0 : RFont2_1;
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, src.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3937 drawFont4Int2 - never blanks the leading digit. Every operator-
        // table call site in frmYMF262.cs passes a literal t=0.
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

        // drawBuff.cs:3537 font4Int2 - dirty-diff wrapper.
        public static void Font4Int2(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int2(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:4035 drawFont4Hex12Bit / :3569 font4Hex12Bit.
        private static void DrawFont4Hex12Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xfff);

            int n = num / 0x100;
            num -= n * 0x100;
            n = n > 0xf ? 0 : n;
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

        public static void Font4Hex12Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex12Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3264 SUSFlag - single-character "-"/"*" flag readout. frmYMF262.cs
        // always calls this with a literal t=1 (unlike YM2413's t=0) - reusing the masked
        // font colour deliberately for this readout regardless of channel mute state; kept
        // as-is, not "fixed".
        public static void SusFlag(PixelScreen screen, int x, int y, int t, ref int oi, int ni)
        {
            if (oi == ni) return;
            DrawFont4(screen, x * 4, y * 4, t, ni == 0 ? "-" : "*");
            oi = ni;
        }

        // drawBuff.cs:4269 drawPanP / :1855 Pan (dirty-diff wrapper). `ntp` mirrors the
        // original's tp parameter (always 0 here, see file header).
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

        // drawBuff.cs:1315 KeyBoard - piano-key highlight + note-name/octave readout (see
        // DrawBuffS5b.cs's KeyBoard comment for the x=32/text-at-296-312 shape).
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

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon (used for the per-4op-pair
        // ConnectSelect flags and the DA/DV ADPCM-related flags).
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs's Kakko - draws a variable-height bracket (top cap + `t` middle
        // segments + bottom cap) used to visually group a 4-operator channel pair. `t` is
        // always 0 in frmYMF262.cs's calls, so this always draws just a 2-row (top+bottom
        // cap only, no middle segment) bracket - kept general since it's cheap to, but no
        // caller here ever needs a taller one.
        public static void Kakko(PixelScreen screen, int x, int y, int t, ref int ot, int nt)
        {
            if (ot == nt) return;

            screen.DrawIntArray(x, y, RKakko.Pixels, 16, nt * 4, 0, 4, 8);
            for (int n = 0; n < t; n++)
            {
                screen.DrawIntArray(x, y + n * 8 + 8, RKakko.Pixels, 16, nt * 4, 8, 4, 8);
            }
            screen.DrawIntArray(x, y + t * 8 + 8, RKakko.Pixels, 16, nt * 4, 16, 4, 8);

            ot = nt;
        }

        // drawBuff.cs:4674 ChYMF262_P - channel badge. Melody (ch<18): number badge, 2-digit
        // ("d2" format, since channel numbers go up to 18 - unlike every other chip's
        // 1-digit badge). Rhythm (18<=ch<23): plain abbreviation.
        private static void ChYmf262P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            if (ch < 18)
            {
                SpriteAtlas src = mask ? RType_1 : RType_0;
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont4(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString("d2"));
                return;
            }

            int t = mask ? 1 : 0;
            switch (ch)
            {
                case 18: DrawFont4(screen, (ch - 18) * 4 * 15 + 4 * 4, y, t, "BD"); break;
                case 19: DrawFont4(screen, (ch - 18) * 4 * 15 + 4 * 4, y, t, "SD"); break;
                case 20: DrawFont4(screen, (ch - 18) * 4 * 15 + 4 * 4, y, t, "TM"); break;
                case 21: DrawFont4(screen, (ch - 18) * 4 * 15 + 3 * 4, y, t, "CYM"); break; // 3 characters
                case 22: DrawFont4(screen, (ch - 18) * 4 * 15 + 4 * 4, y, t, "HH"); break;
            }
        }

        // drawBuff.cs's ChYMF262 - dirty-diff wrapper.
        public static void ChYmf262(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYmf262P(screen, 0, ch < 18 ? 8 + ch * 8 : 8 + 18 * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
