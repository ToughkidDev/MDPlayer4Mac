// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYM2413.cs's screenDrawParams path calls: Volume, VolumeXY, KeyBoard, drawInstNumber,
// SUSFlag, ChYM2413, plus the shared drawFont8/drawFont4/drawFont4Int/drawKbn primitives
// those build on. YM2413 (OPLL, used by MSX-MUSIC/MSX-AUDIO and the NES VRC7 expansion
// chip's near-identical register set) has 9 melody FM channels sharing ONE hardware
// instrument-parameter table (only writable when a channel is set to "user instrument"
// mode - register bank 0x00-0x07) plus 5 fixed-role rhythm channels (BD/SD/TOM/CYM/HH) that
// share 3 operators between them. Unlike YM2612/YM2151, only channel 0's `inst[]` array
// carries the shared operator-table values (registers 0x00-0x07 aren't per-channel) - the
// per-channel `inst[0..3]` are instrument-number/sustain-flags/feedback-flag/volume, not an
// operator table.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: every
// original function's `tp` parameter is dropped/hardcoded to 0 (see VgmEngine.cs's header
// comment - no real-hardware output support). Unlike most other chips' ports though, this
// file DOES keep drawFont4's masked/unmasked `t` sprite-variant parameter (loading both
// rFont_03 and rFont_04) rather than simplifying it away, because frmYM2413.cs's rhythm-row
// channel labels (ChYM2413_P's ch>=9 branch) genuinely pass a `mask`-derived t, unlike every
// other chip's drawFont4 call sites in this port (which all pass a literal 0) - keeping it
// matches the same "ready for whenever mute UI is added" rationale as the RType_0/1 badge
// sprites elsewhere.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYm2413
    {
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4, unmasked, t=0)
        public static SpriteAtlas RFont2_1 = null!; // rFont_04 (drawFont4, masked, t=1)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)

        public static void LoadSprites()
        {
            RVol = SpriteAtlas.Load("rVol_01");
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RFont2_1 = SpriteAtlas.Load("rFont_04");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - YM2413's 9 melody channels only ever call this with c=0
        // (mono).
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

        // drawBuff.cs:1171 VolumeXY - same LED meter as Volume, but x/y are pre-scale-by-4
        // grid coordinates (used for the 5 rhythm channels' volume bars, which sit at a
        // different absolute screen position than the melody channels' rows).
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

        // drawBuff.cs:3789 drawFont4 - unlike most other DrawBuffXxx.cs's simplified
        // single-sprite version, this one keeps the real `t` parameter (see file header).
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

        // drawBuff.cs:3801 drawFont4Int - leading-zero-blanked numeric glyphs. Every call
        // site in frmYM2413.cs passes a literal t=0/k=2, so those are hardcoded here (see
        // drawInstNumber below).
        private static void DrawFont4Int(PixelScreen screen, int x, int y, int num)
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

        // drawBuff.cs:957 drawInstNumber - a plain 2-digit instrument/operator-parameter
        // readout (used for both the per-channel instrument number/volume-index cells and
        // every operator-table cell). x/y are pre-scale-by-4 grid coordinates.
        public static void DrawInstNumber(PixelScreen screen, int x, int y, ref int oi, int ni)
        {
            if (oi == ni) return;
            DrawFont4Int(screen, x * 4, y * 4, ni);
            oi = ni;
        }

        // drawBuff.cs:3264 SUSFlag - a single-character "-"/"*" flag readout (sustain-on/
        // percussive-mode flags in the per-channel instrument cell row). Always called with
        // a literal t=0 in frmYM2413.cs.
        public static void SusFlag(PixelScreen screen, int x, int y, int t, ref int oi, int ni)
        {
            if (oi == ni) return;
            DrawFont4(screen, x * 4, y * 4, t, ni == 0 ? "-" : "*");
            oi = ni;
        }

        // drawBuff.cs:1315 KeyBoard - piano-key highlight + note-name/octave readout, the
        // generic variant (x hardcoded to 32, text at 296/312 - see DrawBuffS5b.cs's
        // KeyBoard comment for how this differs from KeyBoardDCSG).
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

        // drawBuff.cs:4542 ChYM2413_P - channel badge. Melody channels (ch<9) draw a
        // number badge from rType's FIRST 16x8 tile (offset x=0, unlike the
        // AY8910/S5B/YM2151/YM2612 badges which use rType's offset-32 tile - a genuinely
        // different tile region on the same sheet). Rhythm channels (ch 9..13) draw a plain
        // 2-3 letter abbreviation with no badge background at all.
        private static void ChYm2413P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            if (ch < 9)
            {
                SpriteAtlas src = mask ? RType_1 : RType_0;
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
                return;
            }

            int t = mask ? 1 : 0;
            switch (ch)
            {
                case 9: DrawFont4(screen, (ch - 9) * 4 * 15 + 4 * 4, y, t, "BD"); break;
                case 10: DrawFont4(screen, (ch - 9) * 4 * 15 + 4 * 4, y, t, "SD"); break;
                case 11: DrawFont4(screen, (ch - 9) * 4 * 15 + 4 * 4, y, t, "TM"); break;
                case 12: DrawFont4(screen, (ch - 9) * 4 * 15 + 3 * 4, y, t, "CYM"); break; // 3 characters
                case 13: DrawFont4(screen, (ch - 9) * 4 * 15 + 4 * 4, y, t, "HH"); break;
            }
        }

        // drawBuff.cs:2251 ChYM2413 - dirty-diff wrapper. Rhythm channels (9..13) all share
        // row y=8+9*8 (one shared header row below the 9 melody-channel rows), positioned
        // side-by-side within that row by ChYm2413P's per-case x offset above.
        public static void ChYm2413(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYm2413P(screen, 0, ch < 9 ? 8 + ch * 8 : 8 + 9 * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
