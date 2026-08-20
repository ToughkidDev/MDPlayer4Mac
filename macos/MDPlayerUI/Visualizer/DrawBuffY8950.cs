// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmY8950.cs's screenDrawParams path calls: font4Int2, font4Hex12Bit, KeyBoard, VolumeXY,
// ChY8950, drawNESSw, plus the shared drawFont8/drawFont4/drawKbn primitives those build on.
// Y8950 (MSX-AUDIO's FM+ADPCM chip - an OPL1/YM3526 core plus a built-in ADPCM playback
// channel) has the same 9 FM + 5 rhythm channel layout as YM3526/YM3812, plus one extra
// ADPCM channel (index 14: LED volume bar + keyboard + badge, no operator table - it's a
// sample player, not an FM channel).
//
// Layout quirk carried over faithfully from the original: the ADPCM channel's badge sits
// in the row ABOVE the 5 rhythm channels' shared badge row (ChY8950_P: ch<9 -> row ch,
// ch 9..13 (rhythm) -> row 10, ch==14 (ADPCM) -> row 9) - i.e. on screen the ADPCM channel
// visually appears where you'd expect a 10th FM channel, with the rhythm section pushed one
// row further down than YM3526/YM3812's layout. The ADPCM channel's KeyBoard/VolumeXY calls
// in Y8950Visualizer.cs reuse row-index 9's y-coordinate for the same reason (frmY8950.cs's
// own calls do this too: `DrawBuff.KeyBoard(frameBuffer, 9, ...)` - a literal 9, not 14).
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0. Keeps DrawFont4's real masked/unmasked `t` parameter like
// DrawBuffYm2413.cs/DrawBuffYm3526.cs/DrawBuffYm3812.cs, for the same rhythm-row-label
// reason.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffY8950
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
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1171 VolumeXY - x/y are pre-scale-by-4 grid coordinates.
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

        // drawBuff.cs:3789 drawFont4 - real t parameter kept (see file header).
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

        // drawBuff.cs:3937 drawFont4Int2 - never blanks the leading digit. Every call site
        // in frmY8950.cs passes a literal t=0.
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

        // drawBuff.cs:4035 drawFont4Hex12Bit / :3569 font4Hex12Bit (dirty-diff wrapper).
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

        // drawBuff.cs:1315 KeyBoard - piano-key highlight + note-name/octave readout (see
        // DrawBuffS5b.cs's KeyBoard comment for the x=32/text-at-296-312 shape). `c` is the
        // ROW index (not necessarily the channel index - see file header for why the
        // ADPCM channel reuses row 9).
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

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon (DA/DV flags).
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:4606 ChY8950_P - channel badge. Melody (ch<9): number badge, row=ch.
        // Rhythm (9<=ch<14): plain abbreviation, row=10 (NOT row 9 - pushed down one row
        // vs. YM3526/YM3812, see file header). ADPCM (ch==14): a plain wide badge with no
        // text (rType's third tile region, offset x=64, 24px wide - no channel-number/label
        // text drawn over it at all, unlike every other badge in this port), row=9.
        private static void ChY8950P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            if (ch < 9)
            {
                SpriteAtlas src0 = mask ? RType_1 : RType_0;
                screen.DrawIntArray(x, y, src0.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
                return;
            }

            if (ch < 14)
            {
                int t = mask ? 1 : 0;
                switch (ch)
                {
                    case 9: DrawFont4(screen, (ch - 9) * 4 * 15 + 1 * 4, y, t, "BD"); break;
                    case 10: DrawFont4(screen, (ch - 9) * 4 * 15 + 1 * 4, y, t, "SD"); break;
                    case 11: DrawFont4(screen, (ch - 9) * 4 * 15 + 1 * 4, y, t, "TM"); break;
                    case 12: DrawFont4(screen, (ch - 9) * 4 * 15 + 0 * 4, y, t, "CYM"); break; // 3 characters
                    case 13: DrawFont4(screen, (ch - 9) * 4 * 15 + 1 * 4, y, t, "HH"); break;
                }
                return;
            }

            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 64, 0, 24, 8);
        }

        // drawBuff.cs:2263 ChY8950 - dirty-diff wrapper. Row selection: ch<9 -> row ch;
        // 9<=ch<14 (rhythm) -> row 10; ch==14 (ADPCM) -> row 9.
        public static void ChY8950(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            int y = ch < 9 ? 8 + ch * 8 : (ch < 14 ? 8 + 10 * 8 : 8 + 9 * 8);
            ChY8950P(screen, 0, y, ch, nm ?? false);
            om = nm;
        }
    }
}
