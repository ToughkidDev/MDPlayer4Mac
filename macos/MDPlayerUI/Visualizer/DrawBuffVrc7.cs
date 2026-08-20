// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmVRC7.cs's screenDrawParams path calls. VRC7 is an NES cartridge mapper chip built
// around a cut-down YM2413 (OPLL) core: 6 melody FM channels, no rhythm channels (unlike
// YM2413's 9 melody + 5 rhythm split), and only channel 0's register bank carries the
// shared operator-parameter table (registers are per-channel note/instrument-number/
// sustain/volume, plus one shared instrument-parameter block). Reuses the exact same
// drawInstNumber/SUSFlag/ChYM2413 primitives as YM2413's own port (duplicated here, not
// cross-referenced, per this port's convention), since VRC7's channels never exceed index 5
// and so always fall into ChYM2413_P's "numbered melody badge" branch.
//
// Data source: reads chipRegister.GetVRC7Register(chipId) (raw register bytes) and
// chipRegister.getVRC7KeyInfo(chipId) (a ChipKeyInfo one-shot-key-on tracker, same pattern
// already used by YM3812/Y8950's ports) - both already existed in ChipRegister.cs; only
// GetVRC7Register needed a new public wrapper (the existing getVRC7Register was marked
// `internal`, unreachable from this UI-layer project) - see that file's comment.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffVrc7
    {
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Int, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)

        public static void LoadSprites()
        {
            RVol = SpriteAtlas.Load("rVol_01");
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - VRC7's 6 melody channels only ever call this with c=0
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

        // drawBuff.cs:3801 drawFont4Int - leading-zero-blanked numeric glyphs. Every call
        // site in frmVRC7.cs passes a literal t=0/k=2, hardcoded here (see DrawInstNumber).
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
        // readout. x/y are pre-scale-by-4 grid coordinates.
        public static void DrawInstNumber(PixelScreen screen, int x, int y, ref int oi, int ni)
        {
            if (oi == ni) return;
            DrawFont4Int(screen, x * 4, y * 4, ni);
            oi = ni;
        }

        // drawBuff.cs:3264 SUSFlag - a single-character "-"/"*" flag readout. Always called
        // with a literal t=0 in frmVRC7.cs.
        public static void SusFlag(PixelScreen screen, int x, int y, int t, ref int oi, int ni)
        {
            if (oi == ni) return;
            DrawFont4(screen, x * 4, y * 4, t, ni == 0 ? "-" : "*");
            oi = ni;
        }

        // drawBuff.cs:1315 KeyBoard - the "row index" overload (tp dropped/hardcoded to 0).
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

        // drawBuff.cs:4542 ChYM2413_P - channel badge, melody-channel (ch<9) branch only;
        // VRC7's 6 channels never reach the rhythm-channel branch.
        private static void ChVrc7P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
            DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:2251 ChYM2413 - dirty-diff wrapper, called DrawBuff.ChYM2413 in
        // frmVRC7.cs (same shared function YM2413 uses, since VRC7's register layout is
        // near-identical) - reimplemented here rather than cross-referencing DrawBuffYm2413.
        public static void ChVrc7(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChVrc7P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
