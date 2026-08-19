// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmSN76489.cs's primary (non-NGP) ScreenDrawParams path actually calls: Volume, Pan,
// KeyBoardDCSG, ChSN76489, ChSN76489Noise, drawFont4, drawFont8, font4Hex12Bit. Everything
// here is a near-literal transcription of the originals (same coordinates, same tile
// geometry - see each function's comment for the drawBuff.cs line it came from), with one
// deliberate, documented simplification: every original function takes a `tp` parameter
// (0=emulated chip, 1=real chip routed through a sound card, 2=real chip with no sound
// card) selecting which of 2-3 color variants of a sprite sheet to use. This port's engine
// (see VgmEngine.cs's header comment) never supports real-hardware output - only the
// default software emulator - so `tp` is always 0 here, and only the tp=0 sprite variant of
// each sheet was exported (see Assets/Visualizer/README.md). If real-hardware support is
// ever added, these functions need a `tp` parameter again and the tp=1/2 sprite sheets
// re-exported.
//
// This subset never calls the original's drawByteArrayTransp (the transparency-colorkey
// blit primitive) - every function used here calls drawIntArray, an unconditional opaque
// copy - so PixelScreen only implements that one primitive. If a future chip visualizer
// needs a sprite with actual transparent holes, drawByteArrayTransp needs porting too (see
// FrameBuffer.cs's original for the colorkey logic: source pixel 0xff00ff00 is skipped).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffSn76489
    {
        // Loaded once by Sn76489Visualizer before first draw.
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RPan = null!; // rPan_01

        public static void LoadSprites()
        {
            RVol = SpriteAtlas.Load("rVol_01");
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RPan = SpriteAtlas.Load("rPan_01");
        }

        // drawBuff.cs:3632 VolumeP - one 2px-wide LED-bar column tile.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - full LED volume-bar meter (20 columns, 0..19 range).
        // c: 0=Mono 1=Stereo(L) 2=Stereo(R)
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

        // drawBuff.cs:3638 drawKbn - one piano-key shape tile (t selects which of the 8
        // key-shape variants: white-key-left/black-key/white-key-right/etc, x2 for
        // unlit/lit via +4).
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

        // drawBuff.cs:3680 drawFont8 - 8px-wide bitmap font (used for channel numbers and
        // the note-name/octave readout). t: 0=unmasked colour, 1=masked colour.
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

        // drawBuff.cs:3789 drawFont4 - 4px-wide bitmap font (used for volume/freq digit
        // readouts and the noise-mode text). t is always 0 in this port (see file header).
        public static void DrawFont4(PixelScreen screen, int x, int y, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:4035 drawFont4Hex12Bit - three hex digits via DrawFont4.
        private static void DrawFont4Hex12Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xfff);

            int n = num / 0x100;
            num -= n * 0x100;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, Tables.hexCh[n]);

            n = num / 0x10;
            num -= n * 0x10;
            n = n > 0xf ? 0 : n;
            x += 4;
            DrawFont4(screen, x, y, Tables.hexCh[n]);

            n = num / 1;
            x += 4;
            DrawFont4(screen, x, y, Tables.hexCh[n]);
        }

        // drawBuff.cs:3569 font4Hex12Bit - dirty-diff wrapper around DrawFont4Hex12Bit.
        public static void Font4Hex12Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex12Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:4269 drawPanP - one 8x8 pan-indicator tile.
        private static void DrawPanP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPan.Pixels, 32, 8 * t, 0, 8, 8);
        }

        // drawBuff.cs:1855 Pan - dirty-diff wrapper. `ntp` mirrors the original's tp
        // parameter (always 0 here, see file header) rather than a second pan value.
        public static void Pan(PixelScreen screen, int x, int y, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;

            DrawPanP(screen, x, y, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4502 ChSN76489_P - channel-number badge + label.
        private static void ChSN76489P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 32, 0, 16, 8);
            DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:2192 ChSN76489 - dirty-diff wrapper (channel mute-mask indicator).
        public static void ChSN76489(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChSN76489P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:1459 KeyBoardDCSG - piano-key highlight + note-name/octave readout.
        public static void KeyBoardDCSG(PixelScreen screen, int x, int y, ref int ot, int nt)
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

            DrawFont8(screen, 288 + x, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 288 + x, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 304 + x, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:2204 ChSN76489Noise - noise channel's MODE:/RATE: dynamic text
        // (static "MODE:"/"RATE:" labels are baked into the planeSN76489 background image;
        // this only draws the two dynamic words on top). osc/nsc.note carries the raw
        // SN76489 noise-control register byte here (see Sn76489Visualizer.ScreenChangeParams),
        // not an actual note number - matches the original's field reuse.
        public static void ChSN76489Noise(PixelScreen screen, ref MDChipParams.Channel osc, MDChipParams.Channel nsc)
        {
            if (osc.note == nsc.note) return;

            DrawFont4(screen, 56, 32, (nsc.note & 0x4) != 0 ? "WHITE   " : "PERIODIC");
            DrawFont4(screen, 120, 32, (nsc.note & 0x3) == 0 ? "0  " : ((nsc.note & 0x3) == 1 ? "1  " : ((nsc.note & 0x3) == 2 ? "2  " : "CH3")));

            osc.note = nsc.note;
        }
    }
}
