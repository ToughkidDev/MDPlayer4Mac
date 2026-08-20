// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmQSound.cs's screenDrawParams path calls. QSound (Capcom's CQ-SPX67610, used across many
// CPS1/CPS2-era arcade boards) is 19 channels - 16 PCM voices plus 3 ADPCM voices - one 8px
// row per channel, showing echo/frequency/bank/sample-start/sample-end/loop-start 16-bit hex
// readouts (PCM channels only), a byte-packed 2-tile pan icon (PCM channels only - the ADPCM
// channels' PanType2 call is commented out in the original), independent L/R volume LED bars
// (raw-pixel-position VolumeXY overload) for all 19 channels, a keyboard/note readout (PCM
// channels only), and a channel badge for all 19. Rows 17-18 additionally show the chip's
// shared echo/reverb unit state (feedback, end position, delay update, next state, and
// wet/dry delay+volume pairs for L/R) in unused columns of the ADPCM channel rows.
//
// Data source: reads chipRegister.getQSoundRegister(chipId) directly - already a public
// ChipRegister method (mirrors Audio.GetQSoundRegister), no new getter needed.
//
// Genuinely-preserved original quirk: the channel badge (ChQSound_P) draws the channel number
// via plain drawFont4 with a "d2"-formatted string, not the n*4+64 special-offset
// DrawFont4Int2 region used by C140/MpcmX68k/PCM8's badges - a different approach to the same
// problem, kept exactly as the original wrote it. Also, ChQSoundAdpcm_P exists in the original
// source but is never actually called anywhere (both the PCM and ADPCM channel loops call the
// same ChQSound, not a distinct ADPCM variant) - genuinely dead code, omitted here rather than
// ported as an unused function.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffQSound
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Hex*, t=0)
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

        // drawBuff.cs:1171 VolumeXY - raw-pixel-position overload (x/y given in 4px cells,
        // multiplied out inside), same shape already ported independently in
        // DrawBuffC352.cs/DrawBuffMultiPCM.cs; duplicated again here.
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

        // drawBuff.cs:4275 drawPanType2P / :1868 PanType2(screen,c,...) - row-index overload
        // (fixed x=24). Same original function already duplicated independently in several
        // other DrawBuffXxx.cs files in this port.
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

        // drawBuff.cs:1782 KeyBoardToQSound - distinct keyboard overload: fixed text x=52*8=416
        // and (52+2)*8=432, octave bound check < 8, only draws the "   " blank-out text when
        // going to a negative note (like KeyBoardToMultiPCM, not the unconditional-then-
        // overwrite shape most other chips' KeyBoard* overloads use).
        public static void KeyBoardToQSound(PixelScreen screen, int row, ref int ot, int nt)
        {
            if (ot == nt) return;

            int y = (row + 1) * 8;

            if (ot >= 0)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, 32 + kx, y, kt);
            }

            const int x = 52;
            if (nt >= 0)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, 32 + kx, y, kt);
                DrawFont8(screen, x * 8, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 8)
                {
                    DrawFont8(screen, (x + 2) * 8, y, 1, Tables.kbo[nt / 12]);
                }
            }
            else
            {
                DrawFont8(screen, x * 8, y, 1, "   ");
            }

            ot = nt;
        }

        // drawBuff.cs:4392 ChQSound_P - badge offset (16,0,16,8), channel number via plain
        // drawFont4 with a "D2"-formatted string (NOT the n*4+64 DrawFont4Int2 special-offset
        // region used by C140/MpcmX68k/PCM8's badges) - a genuinely different approach, kept
        // as the original wrote it.
        private static void ChQSoundP(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 16, 0, 16, 8);
            DrawFont4(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString("D2"));
        }

        // drawBuff.cs:2030 ChQSound - dirty-diff wrapper, used for both the PCM and ADPCM
        // channel loops (there is no distinct ADPCM badge call in the original despite
        // ChQSoundAdpcm_P existing).
        public static void ChQSound(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChQSoundP(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }
    }
}
