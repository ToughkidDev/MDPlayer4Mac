// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYMF271.cs's screenDrawParams path calls. YMF271 (OPX, Yamaha's FM+PCM "wavetable
// synthesis" chip - 4-operator FM voices that can also play back PCM samples) is laid out
// very differently from the OPN-family chips: 48 identical slots in a flat list (no FM/SSG/
// rhythm section split), each with its own instrument row covering both FM envelope params
// (AR/DR/SR/RR/SL/TL/KS/ML/DT/waveform/feedback/accon/algorithm) and PCM playback params
// (start/end/loop address, sample rate/bit-depth/source-note/source-bank) plus an LFO
// section, all drawn on one wide row per slot rather than a grid. No channel-mask badge is
// ever drawn for this chip (frmYMF271.cs never calls a ChYMF271-style function) - the only
// per-slot "identity" readout is a plain slot-number digit pair.
//
// Data source: reads chipRegister.GetYMF271Register(chipID) directly, which - unlike the
// OPN-family chips' raw-register-byte getters - returns the emulation core's own already-
// decoded MDSound.ymf271.YMF271Chip struct tree (slots[]/groups[]), since that's what the
// original Windows Visualizer reads too (see ChipRegister.cs's GetYMF271Register comment).
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYmf271
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Int, t=0)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RPan2 = null!; // rPan2_01 (PanType2's 2-tile pan icon sheet)
        public static SpriteAtlas RTypeYmf271 = null!; // rType_YMF271 (OpxOP's group-sync icon)

        public static void LoadSprites()
        {
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RVol = SpriteAtlas.Load("rVol_01");
            RPan2 = SpriteAtlas.Load("rPan2_01");
            RTypeYmf271 = SpriteAtlas.Load("rType_YMF271");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y. frmYMF271.cs always calls this with c=1
        // for both L and R (the L/R split comes from the caller's y offset, not from c
        // switching to 2 like every other chip in this port) - transcribed faithfully.
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

        // drawBuff.cs:3928 drawFont4Int1 / :3529 font4Int1 - single-digit (mod 10, no
        // padding/blanking) readout, used for KS/DT/waveform/feedback/accon/fs/bits/
        // srcnote/srcb/lfowave/pms/ams (all small enum-ish 0-7ish values).
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

        // drawBuff.cs:3801 drawFont4Int - leading-zero-blanked/zero-padded numeric readout
        // depending on k (k==3: 3-digit zero-padded; k==2: 2-digit zero-padded here, since
        // every frmYMF271.cs call site passes t=0/k=2 or k=3 with values that are never
        // negative - no leading-zero-blank behaviour is actually exercised by this chip).
        private static void DrawFont4Int(PixelScreen screen, int x, int y, int k, int num)
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
            DrawFont4Int(screen, x, y, 2, nn);
            on = nn;
        }

        public static void Font4Int3(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int(screen, x, y, 3, nn);
            on = nn;
        }

        // drawFont4Hex12Bit/font4Hex12Bit - fns's 12-bit fractional-frequency value, 3 hex
        // digits.
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

        // drawBuff.cs:4152 drawFont4Hex24Bit/font4Hex24Bit - PCM start/end/loop address,
        // 24-bit value, 6 hex digits.
        private static void DrawFont4Hex24Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xff_ffff);

            int n = num / 0x10_0000;
            num -= n * 0x10_0000;
            n = n > 0xf ? 0 : n;
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

        public static void Font4Hex24Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex24Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:1389 KeyBoardXY - piano-key highlight + note-name/octave readout,
        // explicit x/y (used directly, unlike KeyBoardOPNA's near-identical body - only the
        // note-label x offsets differ: 264/280 here vs KeyBoardOPNA's 296/312).
        public static void KeyBoard(PixelScreen screen, int x, int y, ref int ot, int nt)
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

            DrawFont8(screen, 264 + x, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 264 + x, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 280 + x, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:4275 drawPanType2P / :1928 PanType2 - byte-packed L/R pan readout
        // (nt low nibble = one weight, high nibble = the other), explicit x/y overload
        // (frmYMF271.cs calls this twice per slot, once for ch0/ch1 level and once for
        // ch2/ch3 level). Two 4x8 icon halves, same rPan2 sheet as YMF278B's PanType2.
        private static void DrawPanType2P(PixelScreen screen, int x, int y, int t)
        {
            int p = t & 0x0f;
            p = p == 0 ? 0 : 1 + p / 4;
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);

            p = (t & 0xf0) >> 4;
            p = p == 0 ? 0 : 1 + p / 4;
            screen.DrawIntArray(x + 4, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
        }

        public static void PanType2(PixelScreen screen, int x, int y, ref int ot, int nt)
        {
            if (ot == nt) return;
            DrawPanType2P(screen, x, y, nt);
            ot = nt;
        }

        // drawBuff.cs:3288 OpxOP - group-sync mode icon (one per 4-slot group, drawn once
        // every 4th slot), single 8x32 sprite cell selected by the sync value (0-3).
        public static void OpxOP(PixelScreen screen, int x, int y, ref int ot, int nt)
        {
            if (ot == nt) return;
            screen.DrawIntArray(x, y, RTypeYmf271.Pixels, 32, nt * 8, 0, 8, 32);
            ot = nt;
        }
    }
}
