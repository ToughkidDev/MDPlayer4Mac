// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmSegaPCM.cs's screenDrawParams path calls. Sega's SegaPCM (used in many mid-80s Sega
// arcade boards, e.g. Hang-On/OutRun/Space Harrier) is 16 sample-playback channels, one 8px
// row per channel, each showing a keyboard/note readout, independent L/R volume LED bars, a
// byte-packed 2-tile pan icon, and a channel badge.
//
// Data source: reads chipRegister.pcmRegisterSEGAPCM[chipId] (a 0x200-byte flat register
// image) and chipRegister.pcmKeyOnSEGAPCM[chipId] (a 16-entry live key-on flag array)
// directly - both are already public ChipRegister fields (Audio.GetSEGAPCMRegister/
// GetSEGAPCMKeyOn just forward to the same fields on Windows), so no new getter was needed
// for this chip, same shape as C140's pcmRegisterC140/pcmKeyOnC140.
//
// Genuinely-preserved original quirk: the channel badge (ChSegaPCM_P) draws the channel
// number via plain drawFont4 with a "d2"-formatted string at a 16px badge-icon offset, the
// same approach QSound's ChQSound_P uses (not the n*4+64 DrawFont4Int2 special-offset region
// used by C140/MpcmX68k/PCM8's badges) - kept exactly as the original wrote it.
//
// Deliberate simplification: `tp` hardcoded 0 (frmSegaPCM.cs derives it from a "use real
// hardware" UI setting - SEGAPCMType[chipID].UseReal[0] - that this port's Setting.cs does
// carry as a field, but no visualizer in this port wires per-chip UI settings into its
// drawing path), same as every other DrawBuffXxx.cs in this port. screenInit's placeholder
// pre-draw loop is skipped, same as every other chip in this port - the first real
// ScreenDrawParams call naturally draws everything via the normal dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffSegaPCM
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4, unmasked, t=0)
        public static SpriteAtlas RFont2_1 = null!; // rFont_04 (drawFont4, masked, t=1 - for the badge digits)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan2 = null!; // rPan2_01 (2-tile byte-packed L/R pan icon)

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
            RPan2 = SpriteAtlas.Load("rPan2_01");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y, fixed x=256, called with c=1/c=2 for L/R.
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
            SpriteAtlas src = t == 0 ? RFont2_0 : RFont2_1;
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, src.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:1315 KeyBoard - the plain row-index overload (fixed x=296/312), shared
        // verbatim by many chips (already ported independently elsewhere in this port);
        // duplicated again here per the file-independence convention.
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
        // overload (fixed x=24). Same original function already duplicated independently in
        // several other DrawBuffXxx.cs files in this port.
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

        // drawBuff.cs:4492 ChSegaPCM_P - badge offset (16,0,16,8), channel number via plain
        // drawFont4 with a "D2"-formatted string (same approach QSound's ChQSound_P uses, NOT
        // the n*4+64 DrawFont4Int2 special-offset region used by C140/MpcmX68k/PCM8's badges),
        // kept exactly as the original wrote it.
        private static void ChSegaPcmP(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 16, 0, 16, 8);
            DrawFont4(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString("D2"));
        }

        // drawBuff.cs:2180 ChSegaPCM - dirty-diff wrapper.
        public static void ChSegaPcm(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChSegaPcmP(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
