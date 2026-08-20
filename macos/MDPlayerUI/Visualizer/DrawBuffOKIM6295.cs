// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmOKIM6295.cs's screenDrawParams path calls. OKIM6295 (OKI MSM6295, a widely-used ADPCM
// sample player in arcade boards) is 4 channels, one 8px row per channel, each showing a
// channel badge, a single-column volume LED bar (no L/R split - this chip's output isn't
// panned), and 20-bit sample start/end address hex readouts. Below the 4 channel rows: a
// 32-bit master clock hex readout, a pin7 state hex readout, and 4 NMK112 bank-select hex
// readouts (one per channel, used by NMK's bank-switching variant of this chip).
//
// Data source: reads chipRegister.GetOKIM6295Info(chipId) - already present in ChipRegister.cs
// but as `internal`, which MDPlayerUI (a separate assembly from MDPlayerCore) can't see;
// widened to `public` alongside this file rather than adding a duplicate wrapper.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0, and screenInitOKIM6295's placeholder pre-draw work (there isn't any
// in the original - screenInit is an empty method here) needs no equivalent.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffOKIM6295
    {
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Hex*, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;

        public static void LoadSprites()
        {
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y, called with c=0 here (single column, no
        // L/R split: t stays 0, sy stays 0).
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

        // drawBuff.cs:3789 drawFont4 - this chip only ever passes t=0.
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

        // drawBuff.cs:4115 drawFont4Hex20Bit / :3593 font4Hex20Bit - same shape already ported
        // independently in DrawBuffGA20.cs; duplicated again here.
        private static void DrawFont4Hex20Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xf_ffff);

            int n = num / 0x1_0000;
            num -= n * 0x1_0000;
            n = n > 0xf ? 0 : n;
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

        public static void Font4Hex20Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex20Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:4152 drawFont4Hex24Bit / :3601 font4Hex24Bit - not used by this chip's
        // 20-bit addresses, but font4Hex32Bit (master clock) is needed instead - see below.

        // drawBuff.cs:4195 drawFont4Hex32Bit / :3609 font4Hex32Bit - same shape already ported
        // independently in DrawBuffMpcmX68k.cs; duplicated again here.
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

        // drawBuff.cs:4437 ChOKIM6295_P - badge offset (64,0,24,8), standard drawFont8 "d1"
        // channel-number form (up to 4 channels, no zero-pad needed).
        private static void ChOKIM6295P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 64, 0, 24, 8);
            DrawFont8(screen, x + 24, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:2096 ChOKIM6295 - dirty-diff wrapper.
        public static void ChOKIM6295(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChOKIM6295P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
