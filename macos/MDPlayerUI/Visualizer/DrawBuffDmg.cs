// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmDMG.cs's screenDrawParams path calls. DMG is the original Game Boy's 4-channel sound
// hardware: 2 pulse/square channels with duty+envelope+sweep (channel 0 only has sweep),
// 1 custom-wavetable channel (32 4-bit samples, drawn as a 2-row bar-graph strip), and 1
// noise channel. Each channel has its own hand-placed layout (closer to NESDMC/FDS/MMC5's
// bespoke style than N106/VRC6's uniform grids), plus a shared stereo pan readout per
// channel (DMG's stereo panning predates every other NES-family chip ported so far).
//
// Data source: reads chipRegister.GetDMGRegister(chipId) - MDSound.gb.gb_sound_t, a
// one-line forward added to ChipRegister.cs alongside this file (same shape as
// GetYMF271Register) since mds.ReadDMG already existed with no wrapper.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffDmg
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Int*, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (on/off switch icon)
        public static SpriteAtlas RPan = null!; // rPan_01 (stereo pan indicator)
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
            RNesDmc = SpriteAtlas.Load("rNESDMC");
            RPan = SpriteAtlas.Load("rPan_01");
            RWavGraph = SpriteAtlas.Load("rWavGraph");
        }

        // drawBuff.cs:4269 drawPanP - one 8x8 pan-indicator tile.
        private static void DrawPanP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPan.Pixels, 32, 8 * t, 0, 8, 8);
        }

        // drawBuff.cs:1855 Pan - dirty-diff wrapper. `ntp` mirrors the original's tp
        // parameter (always the literal 0 at every frmDMG.cs call site, not a second pan
        // value) rather than being folded away - kept so the dirty-diff still exactly
        // matches the original's `ot == nt && otp == ntp` gate.
        public static void Pan(PixelScreen screen, int x, int y, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;

            DrawPanP(screen, x, y, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1171 VolumeXY - same LED meter as Volume, but x/y are pre-scale-by-4
        // grid coordinates (every frmDMG.cs call site uses c=1, i.e. the "left"/high half of
        // the shared meter palette - DMG's volumeL/volumeR are separate L/R channel values,
        // not a mono/stereo selector).
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

        // drawBuff.cs:3789 drawFont4.
        private static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3928 drawFont4Int1/font4Int1 - single unpadded digit (num % 10).
        private static void DrawFont4Int1(PixelScreen screen, int x, int y, int num)
        {
            int n = num % 10;
            screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
        }

        public static void Font4Int1(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int1(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3937 drawFont4Int2/font4Int2 - genuinely-duplicate body shared with
        // drawFont4Int3 (both branch on k==3 for 3-digit vs default 2-digit; k=1 and k=2
        // hit the identical "else" branch, so frmDMG.cs's k=1 call sites behave exactly like
        // every other chip's k=2 call sites - no separate k=1 wrapper needed).
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

        // drawBuff.cs:1566 KeyBoardDMG - DMG-specific note-label x offsets (312/328), unlike
        // the generic row-index KeyBoard's 296/312 (see NESDMC/FDS/MMC5/VRC7's KeyBoard
        // comment for that variant).
        public static void KeyBoardDMG(PixelScreen screen, int row, ref int ot, int nt)
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

            DrawFont8(screen, 312, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 312, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 328, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon.
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:2726 WaveFormToDMG - draws the wavetable channel's 32 4-bit samples
        // (0-15) as a 2-row bar-graph strip (unlike FDS's 4-row/5-bit version - DMG's
        // wavetable RAM is only 4 bits per sample).
        public static void WaveFormToDMG(PixelScreen screen, int x, int y, ref byte[] oi, byte[] ni)
        {
            for (int i = 0; i < 32; i++)
            {
                if (oi[i] == ni[i]) continue;

                int n = ni[i];

                int m = n > 7 ? 8 : n;
                screen.DrawIntArray(x + i, y, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 15 ? 8 : (n - 8 < 0 ? 0 : n - 8);
                screen.DrawIntArray(x + i, y - 8, RWavGraph.Pixels, 64, m, 0, 1, 8);

                oi[i] = ni[i];
            }
        }

        // drawBuff.cs:4998 ChDMG_P - 2 numbered pulse-channel badges at rows 1/2, 1
        // unnumbered wide wavetable-channel badge at row 3, 1 unnumbered noise-channel
        // badge at row 4 (all uniform 8px-tall rows, unlike NESDMC/FDS/MMC5's scattered
        // layout).
        private static void ChDmgP(PixelScreen screen, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            switch (ch)
            {
                case 0:
                    screen.DrawIntArray(0, 8, src.Pixels, 128, 48, 8, 16, 8);
                    DrawFont8(screen, 16, 8, mask ? 1 : 0, "1");
                    break;
                case 1:
                    screen.DrawIntArray(0, 16, src.Pixels, 128, 48, 8, 16, 8);
                    DrawFont8(screen, 16, 16, mask ? 1 : 0, "2");
                    break;
                case 2:
                    screen.DrawIntArray(0, 24, src.Pixels, 128, 112, 0, 16, 8);
                    break;
                case 3:
                    screen.DrawIntArray(0, 32, src.Pixels, 128, 96, 8, 24, 8);
                    break;
            }
        }

        // drawBuff.cs:2548 ChDMG - dirty-diff wrapper.
        public static void ChDmg(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChDmgP(screen, ch, nm ?? false);
            om = nm;
        }
    }
}
