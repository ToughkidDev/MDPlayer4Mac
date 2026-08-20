// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmK053260.cs's screenDrawParams path calls. K053260 (Konami's 4-channel PCM sample player,
// paired with a Z80 in many Konami arcade boards) is laid out one 8px row per channel across 4
// channels, each showing hex readouts of frequency/bank/start/size/pan/volume, 4 on/off flag
// icons (play/dir/loop/ppcm), a pair of single-tile pan icons (L and R drawn separately, unlike
// the byte-packed 2-tile PanType2 used by C140/C352), independent L/R volume LED bars (via the
// raw-pixel VolumeXY1 overload), a keyboard/note readout with an explicit x/font-x split
// (KeyBoardXYFX, distinct from every other chip's row-index or fixed-x overloads), and a
// channel badge.
//
// Data source: reads chipRegister.GetK053260Register(chipId) - a new one-line forward added to
// ChipRegister.cs alongside this file, forwarding to mds.getK053260State (mirroring
// Audio.GetK053260Register, which calls the equivalent static mds directly - Windows' Audio.cs
// holds mds as a static field, but this port's ChipRegister keeps it private, so a wrapper was
// needed here just like GetHuC6280Register/GetDMGRegister).
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffK053260
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Hex*, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan2 = null!; // rPan2_01 (single-tile pan icon)
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (on/off switch icon)

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
            RNesDmc = SpriteAtlas.Load("rNESDMC");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1197 VolumeXY1 - raw-pixel-position overload, same shape as C352's
        // VolumeXY but WITHOUT the *4 x/y scaling (the caller already passes final pixel
        // coordinates here).
        public static void VolumeXY1(PixelScreen screen, int x, int y, int c, ref int ov, int nv)
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
        private static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
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

        // drawBuff.cs:4060 drawFont4Hex16Bit / :3577 font4Hex16Bit.
        private static void DrawFont4Hex16Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xffff);

            int n = num / 0x1000;
            num -= n * 0x1000;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x100;
            num -= n * 0x100;
            n = n > 0xf ? 0 : n;
            x += 4;
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

        public static void Font4Hex16Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex16Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:1424 KeyBoardXYFX - explicit x (keyboard graphic origin) / fx (note-name
        // text x) / y overload, distinct from every other chip's row-index or fixed-x
        // overloads ported so far.
        public static void KeyBoardXYFX(PixelScreen screen, int x, int fx, int y, ref int ot, int nt)
        {
            if (ot == nt) return;

            if (ot >= 0 && ot < 12 * 8)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, x + kx, y, kt);
            }

            if (nt >= 0 && nt < 12 * 8)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, x + kx, y, kt);
            }

            DrawFont8(screen, fx, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, fx, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 16 + fx, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:4321 drawPanType5P / :1904 PanType5 - a single 4x8 pan tile (unlike
        // PanType2's byte-packed pair of tiles) - K053260 draws L and R as two independent
        // PanType5 calls instead.
        private static void DrawPanType5P(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, t * 4, 0, 4, 8);
        }

        public static void PanType5(PixelScreen screen, int x, int y, ref int ot, int nt)
        {
            if (ot == nt) return;
            DrawPanType5P(screen, x, y, nt);
            ot = nt;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon.
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:4411 ChC352_P, reused as-is by frmK053260.cs's screenDrawParams (at
        // x=1 instead of C352/GA20's x=0) - same badge offset/2-digit font as C140/C352/GA20.
        private static void ChK053260P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 16, 0, 16, 8);
            DrawFont4(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString("d2"));
        }

        // drawBuff.cs:2072 ChK053260 - dirty-diff wrapper, x fixed at 1 (not 0).
        public static void ChK053260(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChK053260P(screen, 1, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
