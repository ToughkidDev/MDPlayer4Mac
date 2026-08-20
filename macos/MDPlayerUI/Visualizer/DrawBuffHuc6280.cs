// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmHuC6280.cs's screenDrawParams path calls. HuC6280 is the PC Engine/TurboGrafx-16's
// built-in PSG: 6 wavetable channels laid out 3-per-row across 2 rows (channels 0-2 on row
// 0, 3-5 on row 1 - WaveFormToHuC6280/DDAToHuC6280/etc.'s `c > 2` branch), each with its own
// 32-sample waveform display; channels 4-5 additionally have a noise generator.
//
// Data source: reads chipRegister.GetHuC6280Register(chipId) - MDSound.Ootake_PSG.
// huc6280_state, a one-line forward added to ChipRegister.cs alongside this file (same
// shape as GetDMGRegister) since mds.ReadHuC6280Status already existed with no wrapper.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffHuc6280
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan2 = null!; // rPan2_01 (2-tile byte-packed L/R pan icon)
        public static SpriteAtlas RWavGraph = null!; // rWavGraph (wavetable bar-graph column)

        public static void LoadSprites()
        {
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
            RPan2 = SpriteAtlas.Load("rPan2_01");
            RWavGraph = SpriteAtlas.Load("rWavGraph");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1089 VolumeToHuC6280 - fixed x=256, c selects L(1)/R(2) meter half.
        public static void VolumeToHuC6280(PixelScreen screen, int y, int c, ref int ov, int nv)
        {
            if (ov == nv) return;

            int t = 0;
            int sy = 0;
            if (c == 1 || c == 2) { t = 4; }
            if (c == 2) { sy = 4; }
            y = (y + 1) * 8;

            for (int i = 0; i <= 19; i++)
            {
                VolumeP(screen, 256 + i * 2, y + sy, 1 + t);
            }

            for (int i = 0; i <= nv; i++)
            {
                VolumeP(screen, 256 + i * 2, y + sy, i > 17 ? 2 + t : 0 + t);
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
        public static void DrawFont4(PixelScreen screen, int x, int y, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:1315 KeyBoard(screen, y, ref ot, nt, tp) - the "row index" overload
        // (tp dropped/hardcoded to 0, same as every NES-family chip in this port).
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

        // drawBuff.cs:4275 drawPanType2P / :1868 PanType2(screen,c,...) - the row-index
        // overload (fixed x=24, unlike YMF271's explicit-x/y PanType2 overload - two
        // distinct call shapes of the same original function name, reimplemented separately
        // per this port's file-independence convention).
        private static void DrawPanType2P(PixelScreen screen, int x, int y, int t)
        {
            int p = t & 0x0f;
            p = p == 0 ? 0 : 1 + p / 4;
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
            p = (t & 0xf0) >> 4;
            p = p == 0 ? 0 : 1 + p / 4;
            screen.DrawIntArray(x + 4, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
        }

        public static void PanType2(PixelScreen screen, int c, ref int ot, int nt)
        {
            if (ot == nt) return;
            DrawPanType2P(screen, 24, 8 + c * 8, nt);
            ot = nt;
        }

        // drawBuff.cs:2638 WaveFormToHuC6280 - draws a channel's 32-sample waveform as a
        // 4-row bar-graph column; channels 0-2 sit on the upper half of the instrument
        // grid, 3-5 on the lower half (the `c > 2` split below).
        public static void WaveFormToHuC6280(PixelScreen screen, int c, ref int[] oi, int[] ni)
        {
            for (int i = 0; i < 32; i++)
            {
                if (oi[i] == ni[i]) continue;

                int n = 17 - ni[i];
                int x = i + (c > 2 ? c - 3 : c) * 8 * 13 + 4 * 7;
                int y = (c > 2 ? 1 : 0) * 8 * 5 + 4 * 22;

                int m = n > 7 ? 8 : n;
                screen.DrawIntArray(x, y, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 15 ? 8 : (n - 8 < 0 ? 0 : n - 8);
                screen.DrawIntArray(x, y - 8, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 23 ? 8 : (n - 16 < 0 ? 0 : n - 16);
                screen.DrawIntArray(x, y - 16, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 31 ? 8 : (n - 24 < 0 ? 0 : n - 24);
                screen.DrawIntArray(x, y - 23, RWavGraph.Pixels, 64, m + 1, 0, 1, 7);

                oi[i] = ni[i];
            }
        }

        // drawBuff.cs:2780 DDAToHuC6280 - "ON "/"OFF" DDA (direct D/A sample playback)
        // status readout, shares the same c>2 row-split position math as the waveform.
        public static void DDAToHuC6280(PixelScreen screen, int c, ref bool od, bool nd)
        {
            if (od == nd) return;

            int x = (c > 2 ? c - 3 : c) * 8 * 13 + 4 * 22;
            int y = (c > 2 ? 1 : 0) * 8 * 5 + 4 * 18;

            DrawFont4(screen, x, y, nd ? "ON " : "OFF");
            od = nd;
        }

        // drawBuff.cs:2791 NoiseToHuC6280 - "ON "/"OFF" noise-generator status readout
        // (channels 4-5 only).
        public static void NoiseToHuC6280(PixelScreen screen, int c, ref bool od, bool nd)
        {
            if (od == nd) return;

            int x = (c > 2 ? c - 3 : c) * 8 * 13 + 4 * 22;
            int y = (c > 2 ? 1 : 0) * 8 * 5 + 4 * 20;

            DrawFont4(screen, x, y, nd ? "ON " : "OFF");
            od = nd;
        }

        // drawBuff.cs:2802 NoiseFrqToHuC6280 - 2-digit noise-frequency readout.
        public static void NoiseFrqToHuC6280(PixelScreen screen, int c, ref int od, int nd)
        {
            if (od == nd) return;

            int x = (c > 2 ? c - 3 : c) * 8 * 13 + 4 * 22;
            int y = (c > 2 ? 1 : 0) * 8 * 5 + 4 * 22;

            DrawFont4(screen, x, y, nd.ToString("d2"));
            od = nd;
        }

        // drawBuff.cs:2813 MainVolumeToHuC6280 - 2-digit main volume readout (c: 0=L, 1=R).
        public static void MainVolumeToHuC6280(PixelScreen screen, int c, ref int od, int nd)
        {
            if (od == nd) return;

            int x = 8 * 9;
            int y = c * 8 + 8 * 17;

            DrawFont4(screen, x, y, nd.ToString("d2"));
            od = nd;
        }

        // drawBuff.cs:2824 LfoCtrlToHuC6280 - 1-digit LFO control readout.
        public static void LfoCtrlToHuC6280(PixelScreen screen, ref int od, int nd)
        {
            if (od == nd) return;

            DrawFont4(screen, 8 * 17, 8 * 17, nd.ToString("d1"));
            od = nd;
        }

        // drawBuff.cs:2835 LfoFrqToHuC6280 - 3-digit LFO frequency readout.
        public static void LfoFrqToHuC6280(PixelScreen screen, ref int od, int nd)
        {
            if (od == nd) return;

            DrawFont4(screen, 8 * 16, 8 * 18, nd.ToString("d3"));
            od = nd;
        }

        // drawBuff.cs:4429 ChHuC6280_P - single numbered badge per channel, one per 8px row.
        private static void ChHuc6280P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 112, 0, 16, 8);
            DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:2084 ChHuC6280 - dirty-diff wrapper.
        public static void ChHuc6280(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChHuc6280P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
