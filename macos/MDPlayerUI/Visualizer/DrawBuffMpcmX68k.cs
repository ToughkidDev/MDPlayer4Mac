// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmMpcmX68k.cs's screenDrawParams path calls. MpcmX68k is the X68000's ADPCM/PCM sample
// subsystem (the "mpcmX68k" or "mpcmpp" chip model, chosen per format - see
// MpcmX68kVisualizer.cs's header for why this port only ever exercises the mpcmpp path). 16
// channels, one 8px row per channel, each showing a channel badge, a raw 2-tile pan icon, a
// keyboard/note readout (explicit x/font-x split, same KeyBoardXYFX shape as K053260's),
// hex readouts of the sample pointer/size/start/end/count/pitch (32-bit) and PCM type (byte),
// a text readout of the sample rate/format string, and independent L/R volume LED bars.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffMpcmX68k
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Hex*/Int2, t=0)
        public static SpriteAtlas RFont2_1 = null!; // rFont_04 (drawFont4, masked, t=1)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan = null!; // rPan_01 (raw single-tile stereo pan indicator)

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
            RPan = SpriteAtlas.Load("rPan_01");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y, called with c=1/c=2 for L/R.
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

        // drawBuff.cs:3789 drawFont4. t: 0=unmasked, 1=masked (this chip uses masked drawFont4
        // for the frqStr readout, unlike most other chips which only ever pass t=0 here).
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

        // drawBuff.cs:4195 drawFont4Hex32Bit / :3609 font4Hex32Bit.
        private static void DrawFont4Hex32Bit(PixelScreen screen, int x, int y, uint num)
        {
            num = Common.Range(num, 0, 0xffff_ffff);

            uint n = num / 0x1000_0000;
            num -= n * 0x1000_0000;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x100_0000;
            num -= n * 0x100_0000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x10_0000;
            num -= n * 0x10_0000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x1_0000;
            num -= n * 0x1_0000;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, 0, Tables.hexCh[n]);

            n = num / 0x1000;
            num -= n * 0x1000;
            n = n > 0xf ? 0 : n;
            x += 4;
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

        public static void Font4Hex32Bit(PixelScreen screen, int x, int y, ref uint on, uint nn)
        {
            if (on == nn) return;
            DrawFont4Hex32Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3937 drawFont4Int2 (zero-padded 2-digit) - used here for the channel
        // badge's decimal channel number.
        private static void DrawFont4Int2(PixelScreen screen, int x, int y, int t, int num)
        {
            int n = num / 10;
            num -= n * 10;
            n = n > 9 ? 0 : n;
            screen.DrawIntArray(x, y, (t == 0 ? RFont2_0 : RFont2_1).Pixels, 128, n * 4 + 64, 0, 4, 8);

            n = num / 1;
            x += 4;
            screen.DrawIntArray(x, y, (t == 0 ? RFont2_0 : RFont2_1).Pixels, 128, n * 4 + 64, 0, 4, 8);
        }

        // drawBuff.cs:1424 KeyBoardXYFX - explicit x (keyboard graphic origin) / fx (note-name
        // text x) / y overload, same shape already ported independently in
        // DrawBuffK053260.cs; duplicated again here per the file-independence convention.
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

        // drawBuff.cs:4269 drawPanP / :1855 Pan - raw single-tile pan indicator (0-8 range,
        // the same rPan_01 sprite already used by DrawBuffSn76489.cs/DrawBuffDmg.cs), tracked
        // with a second dummy dirty-diff variable (otp/ntp) mirroring the original's ref otp
        // parameter even though this chip never varies ntp (always passes a literal 0/tp).
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

        // drawBuff.cs:4453 ChMPCMX68k_P - wide 24px badge (this chip has up to 16 channels,
        // needing 2 decimal digits) with drawFont4Int2 for the channel number, unlike most
        // other chips' drawFont8/drawFont4 "d2" string forms.
        private static void ChMpcmX68kP(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 64, 0, 24, 8);
            DrawFont4Int2(screen, x + 24, y, mask ? 1 : 0, 1 + ch);
        }

        // drawBuff.cs:2120 ChMPCMX68k - dirty-diff wrapper.
        public static void ChMpcmX68k(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChMpcmX68kP(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
