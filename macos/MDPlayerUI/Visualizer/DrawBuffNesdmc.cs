// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmNESDMC.cs's screenDrawParams path calls. NESDMC is the NES/Famicom's built-in APU: 2
// pulse/square channels, 1 triangle channel, 1 noise channel, and 1 DMC (delta-modulation
// sample playback) channel - 5 channels total, each with its own fixed on-screen position
// rather than a uniform per-row grid (this chip's layout is the smallest/most bespoke of
// any ported so far).
//
// Data source: reads chipRegister.GetAPURegister(chipID)/GetDMCRegister(chipID) - raw
// register byte arrays, added to ChipRegister.cs alongside this file (see its comment) since
// no earlier chip needed them.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffNesdmc
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Int, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (duty-cycle icon + on/off switch icon)

        public static void LoadSprites()
        {
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
            RNesDmc = SpriteAtlas.Load("rNESDMC");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y, always called with c=0 (single bar, no
        // L/R split) by this chip.
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

        // drawBuff.cs:3789 drawFont4.
        public static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3937/3945 drawFont4Int2/drawFont4Int3 - zero-padded numeric readout
        // (k==3: 3-digit; else: 2-digit). Both frmNESDMC.cs call sites use t=0.
        private static void DrawFont4IntPadded(PixelScreen screen, int x, int y, int k, int num)
        {
            if (k == 3)
            {
                int n = num / 100;
                num -= n * 100;
                n = n > 9 ? 0 : n;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);

                n = num / 10;
                num -= n * 10;
                x += 4;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);

                n = num / 1;
                x += 4;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
                return;
            }

            {
                int n = num / 10;
                num -= n * 10;
                n = n > 9 ? 0 : n;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);

                n = num / 1;
                x += 4;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
            }
        }

        public static void Font4Int2(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4IntPadded(screen, x, y, 2, nn);
            on = nn;
        }

        public static void Font4Int3(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4IntPadded(screen, x, y, 3, nn);
            on = nn;
        }

        // drawBuff.cs:4016 drawFont4HexByte / :3561 font4HexByte.
        private static void DrawFont4HexByte(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xff);

            int n = num / 0x10;
            num -= n * 0x10;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 1;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);
        }

        public static void Font4HexByte(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4HexByte(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:1315 KeyBoard(screen, y, ref ot, nt, tp) - the "row index" overload:
        // takes a single row value (not explicit x/y), internally scales to y=(row+1)*8 and
        // always draws the piano keys at fixed x=32 and the note/octave label at fixed
        // x=296/312 (unlike KeyBoardOPNA/KeyBoardXY's per-chip x offsets).
        public static void KeyBoard(PixelScreen screen, int row, ref int ot, int nt)
        {
            if (ot == nt) return;

            int y = (row + 1) * 8;

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

        // drawBuff.cs:3511 drawDuty - pulse-channel duty-cycle icon (0-3), reuses the
        // rNESDMC sheet.
        public static void DrawDuty(PixelScreen screen, int x, int y, ref int op, int np)
        {
            if (op == np) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, np * 8, 0, 8, 8);
            op = np;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon.
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:4945 ChNESDMC_P - each of the 5 channels has its own hand-placed
        // fixed position/size on the small NESDMC layout (not a uniform per-row grid like
        // every other chip in this port): square 1/2 get numbered badges at rows 1/3,
        // triangle gets an unnumbered wide badge at row 5, noise and DMC get their own
        // unnumbered badges at (112,32)/(112,48).
        private static void ChNesdmcP(PixelScreen screen, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            switch (ch)
            {
                case 0:
                    screen.DrawIntArray(0, 8, src.Pixels, 128, 48, 8, 16, 8);
                    DrawFont8(screen, 16, 8, mask ? 1 : 0, "1");
                    break;
                case 1:
                    screen.DrawIntArray(0, 24, src.Pixels, 128, 48, 8, 16, 8);
                    DrawFont8(screen, 16, 24, mask ? 1 : 0, "2");
                    break;
                case 2:
                    screen.DrawIntArray(0, 40, src.Pixels, 128, 64, 8, 32, 8);
                    break;
                case 3:
                    screen.DrawIntArray(112, 32, src.Pixels, 128, 96, 8, 24, 8);
                    break;
                case 4:
                    screen.DrawIntArray(112, 48, src.Pixels, 128, 0, 16, 16, 8);
                    break;
            }
        }

        // drawBuff.cs:2512 ChNESDMC - dirty-diff wrapper.
        public static void ChNesdmc(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChNesdmcP(screen, ch, nm ?? false);
            om = nm;
        }
    }
}
