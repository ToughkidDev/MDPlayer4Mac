// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmFDS.cs's screenDrawParams path calls. FDS (Famicom Disk System) adds one extra
// wavetable-synthesis channel on top of the NES APU, with a graphical 32-sample carrier
// waveform + 32-sample modulation waveform display (reusing the same rWavGraph bar-graph
// sprite sheet as YM2609's PSG wavetable display) plus a block of envelope/LFO parameter
// readouts.
//
// Data source: reads chipRegister.GetFDSRegister(chipId) - MDSound.np.np_nes_fds.NES_FDS,
// added to ChipRegister.cs alongside this file (see its comment), mirroring the
// GetAPURegister/GetDMCRegister NSF-direct/VGM-fallback shape added for NESDMC.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffFds
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Int*/drawFont4Hex12Bit, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (on/off switch icon, shared across NES-family chips)
        public static SpriteAtlas RWavGraph = null!; // rWavGraph (waveform bar-graph column, shared with YM2609's PSG display)

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
            RWavGraph = SpriteAtlas.Load("rWavGraph");
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

        // drawBuff.cs:3937/3970 drawFont4Int2/drawFont4Int3 - genuinely-duplicate bodies in
        // the original (both branch on k==3 for 3-digit vs default 2-digit zero-padded
        // readout); frmFDS.cs's call sites always pass t=0, and always the same k per call
        // site, so this port hardcodes k via two thin wrappers instead of threading it through.
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

        // drawBuff.cs:4035 drawFont4Hex12Bit / font4Hex12Bit.
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

        // drawBuff.cs:1315 KeyBoard(screen, y, ref ot, nt, tp) - the "row index" overload
        // (tp dropped/hardcoded to 0, same as NESDMC's port of the same function).
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

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon, shared across NES-family chips.
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:2688 WaveFormToFDS - draws the 32-sample carrier (c=0) or modulation
        // (c=1) waveform as a column of stacked 4-row bar-graph blocks, each block value
        // 0-31 split across 4 vertical eighths via the rWavGraph sprite sheet.
        public static void WaveFormToFDS(PixelScreen screen, int c, ref int[] oi, int[] ni)
        {
            for (int i = 0; i < 32; i++)
            {
                if (oi[i] == ni[i]) continue;

                int n = ni[i];
                int x = i + c * 4 * 31 + 8;
                int y = 8 * 6;

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

        // drawBuff.cs:4971 ChFDS_P - single-channel unnumbered wide badge at row 0.
        private static void ChFdsP(PixelScreen screen, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(0, 8, src.Pixels, 128, 14 * 8, 0 * 8, 16, 8);
        }

        // drawBuff.cs:2524 ChFDS - dirty-diff wrapper.
        public static void ChFds(PixelScreen screen, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChFdsP(screen, nm ?? false);
            om = nm;
        }
    }
}
