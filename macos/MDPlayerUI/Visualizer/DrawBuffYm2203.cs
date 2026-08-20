// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYM2203.cs's screenDrawParams path calls: Volume (raw-pixel-coordinate variant, unlike
// the grid-coordinate VolumeXY used by the OPL-family files), VolumeShort, KeyBoardOPNA,
// Inst, ChYM2203, Ch3YM2203, Slot, font4Hex16Bit, Tn, drawNESSw, font4HexByte, Nfrq, Efrq,
// Etype, plus the shared drawFont8/drawFont4/drawFont4Int/drawKbn primitives those build
// on. YM2203 (OPN, the original FM+SSG chip - PC-8801/9801FM, MSX-MUSIC's cousin) has 3 FM
// channels (each with a full 4-operator table, unlike the OPL family's 2-operator chips) +
// a "Ch3 extended mode" that splits channel 3 into 3 independently-tunable operator slots
// (shown as 3 extra rows) + 3 SSG (PSG-with-hardware-envelope) tone/noise channels - the
// same SSG core as AY8910/S5B, reusing this port's already-exported rPSGMode_01/rPSGEnv
// sprites.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0. Unlike AY8910/S5B's Tn-equivalent (ToneNoise), frmYM2203.cs's own
// Tn call never adds a mask term to its tp*2 index (`DrawBuff.Tn(frameBuffer, 6, 2, c + 3,
// ..., tp * 2)` - no `+ (mask?1:0)`), so this file's Tn is simpler still: with tp hardcoded
// to 0 the sprite variant is always index 0, and only the unmasked rPSGMode_01 sheet is
// ever needed - kept as-is, not "fixed" to also react to mask like AY8910's does.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYm2203
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4/drawFont4Int, t=0)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RNesDmc = null!;
        public static SpriteAtlas RPsgMode0 = null!; // rPSGMode_01 (tone/noise icon, tp=0)
        public static SpriteAtlas RPsgEnv = null!;

        public static void LoadSprites()
        {
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RVol = SpriteAtlas.Load("rVol_01");
            RNesDmc = SpriteAtlas.Load("rNESDMC");
            RPsgMode0 = SpriteAtlas.Load("rPSGMode_01");
            RPsgEnv = SpriteAtlas.Load("rPSGEnv");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - RAW pixel x/y (unlike VolumeXY's pre-scale-by-4 grid
        // coordinates - the original doc-comments this explicitly as "x座標(x1)").
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

        // drawBuff.cs:1000 VolumeShort - shorter (14-segment) bar, also raw pixel x/y, used
        // for the SSG channels' volume readout.
        public static void VolumeShort(PixelScreen screen, int x, int y, int c, ref int ov, int nv)
        {
            if (ov == nv) return;

            int t = 0;
            int sy = 0;
            if (c == 1 || c == 2) { t = 4; }
            if (c == 2) { sy = 4; }

            for (int i = 0; i <= 15; i++)
            {
                VolumeP(screen, x + i * 2, y + sy, 1 + t);
            }

            for (int i = 0; i <= nv; i++)
            {
                VolumeP(screen, x + i * 2, y + sy, i > 13 ? 2 + t : 0 + t);
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

        // drawBuff.cs:3789 drawFont4 - every call site in frmYM2203.cs's own code (via the
        // hex/int helpers below) uses t=0, so this file only loads the unmasked sheet.
        public static void DrawFont4(PixelScreen screen, int x, int y, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3801 drawFont4Int - numeric glyphs with LEADING-ZERO BLANKING (unlike
        // Font4Int2's always-zero-padded 2-digit readout used elsewhere in this port): the
        // tens digit is only drawn once it's non-zero. k==3 draws a leading-zero-blanked
        // 3-digit number (TL, 0..127); any other k draws leading-zero-blanked 2 digits
        // (every other operator parameter, and Nfrq's 0..31 readout via k=2).
        public static void DrawFont4Int(PixelScreen screen, int x, int y, int k, int num)
        {
            if (k == 3)
            {
                bool f = false;
                int n = num / 100;
                num -= n * 100;
                n = n > 9 ? 0 : n;
                if (n != 0)
                {
                    screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, n * 4 + 64, 0, 4, 8);
                    f = true;
                }
                else
                {
                    screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, 0, 0, 4, 8);
                }

                n = num / 10;
                num -= n * 10;
                x += 4;
                if (n != 0 || f)
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
                return;
            }

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
        }

        // drawBuff.cs:4016 drawFont4HexByte / :3561 font4HexByte (dirty-diff wrapper).
        private static void DrawFont4HexByte(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xff);

            int n = num / 0x10;
            num -= n * 0x10;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, Tables.hexCh[n]);

            n = num / 1;
            x += 4;
            DrawFont4(screen, x, y, Tables.hexCh[n]);
        }

        public static void Font4HexByte(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4HexByte(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs's drawFont4Hex16Bit / font4Hex16Bit - used for the FM-EX channels'
        // 11-bit F-Num + 3-bit block, packed and displayed as 4 hex digits.
        private static void DrawFont4Hex16Bit(PixelScreen screen, int x, int y, int num)
        {
            num = Common.Range(num, 0, 0xffff);

            int n = num / 0x1000;
            num -= n * 0x1000;
            n = n > 0xf ? 0 : n;
            DrawFont4(screen, x, y, Tables.hexCh[n]);

            n = num / 0x100;
            num -= n * 0x100;
            n = n > 0xf ? 0 : n;
            x += 4;
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

        public static void Font4Hex16Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex16Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3937 drawFont4Int2 (zero-padded 2-digit) - used only by Etype below,
        // distinct from the leading-zero-blanked DrawFont4Int used everywhere else in this
        // file.
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

        // drawBuff.cs:750 Inst - the instrument/operator parameter table: 4 operators' 11
        // params (AR/DR/SR/RR/SL/TL/KS/ML/DT/AM/SG, dirty-diffed per cell, TL uses the
        // 3-digit path) plus the 4 whole-channel params (AL/FB/AMS/FMS) above them. `c`
        // selects which of up to 3 channel slots in the table grid this channel goes in.
        public static void Inst(PixelScreen screen, int x, int y, int c, int[] oi, int[] ni)
        {
            int sx = (c % 3) * 8 * 13 + x * 8;
            int sy = (c / 3) * 8 * 6 + 8 * y;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 11; i++)
                {
                    if (oi[i + j * 11] != ni[i + j * 11])
                    {
                        DrawFont4Int(screen, sx + i * 8 + (i > 5 ? 4 : 0), sy + j * 8, i == 5 ? 3 : 2, ni[i + j * 11]);
                        oi[i + j * 11] = ni[i + j * 11];
                    }
                }
            }

            if (oi[44] != ni[44])
            {
                DrawFont4Int(screen, sx + 8 * 4, sy - 16, 2, ni[44]);
                oi[44] = ni[44];
            }
            if (oi[45] != ni[45])
            {
                DrawFont4Int(screen, sx + 8 * 6, sy - 16, 2, ni[45]);
                oi[45] = ni[45];
            }
            if (oi[46] != ni[46])
            {
                DrawFont4Int(screen, sx + 8 * 8 + 4, sy - 16, 2, ni[46]);
                oi[46] = ni[46];
            }
            if (oi[47] != ni[47])
            {
                DrawFont4Int(screen, sx + 8 * 11, sy - 16, 2, ni[47]);
                oi[47] = ni[47];
            }
        }

        // drawBuff.cs:1494 KeyBoardOPNA - piano-key highlight + note-name/octave readout;
        // takes explicit x/y (unlike this port's other KeyBoard variants which bake in a
        // fixed x=32).
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

            DrawFont8(screen, 296 + x, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 296 + x, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 312 + x, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:945 Slot - 4 small on/off icons (one per operator) showing which
        // operators are currently gated on.
        public static void Slot(PixelScreen screen, int x, int y, ref byte os, byte ns)
        {
            if (os == ns) return;

            screen.DrawIntArray(x + 0, y, RNesDmc.Pixels, 64, ((ns & 1) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 4, y, RNesDmc.Pixels, 64, ((ns & 2) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 8, y, RNesDmc.Pixels, 64, ((ns & 4) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 12, y, RNesDmc.Pixels, 64, ((ns & 8) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);

            os = ns;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon (SSG hardware-envelope-mode
        // flag here).
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:5137 drawTnP / :3214 Tn - tone/noise-mode icon. See file header for
        // why `ntp` is always 0 here (only rPSGMode_01 is ever loaded).
        private static void DrawTnP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPsgMode0.Pixels, 32, 8 * t, 0, 8, 8);
        }

        public static void Tn(PixelScreen screen, int x, int y, int c, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;
            DrawTnP(screen, x * 4, y * 4 + c * 8, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:2596 Nfrq - chip-wide hardware-envelope Frequency-divider readout
        // (raw 5-bit register value, 0..31, leading-zero-blanked). `x`/`y` are pre-scale-
        // by-4 grid coordinates.
        public static void Nfrq(PixelScreen screen, int x, int y, ref int onfrq, int nnfrq)
        {
            if (onfrq == nnfrq) return;
            DrawFont4Int(screen, x * 4, y * 4, 2, nnfrq);
            onfrq = nnfrq;
        }

        // drawBuff.cs:2610 Efrq - chip-wide hardware-envelope Frequency (16-bit register
        // pair) readout, 5-digit decimal.
        public static void Efrq(PixelScreen screen, int x, int y, ref int oefrq, int nefrq)
        {
            if (oefrq == nefrq) return;
            DrawFont4(screen, x * 4, y * 4, nefrq.ToString("D5"));
            oefrq = nefrq;
        }

        // drawBuff.cs:2624/4262 Etype/drawEtypeP - chip-wide hardware-envelope Type (0..15)
        // readout: icon + zero-padded 2-digit number.
        public static void Etype(PixelScreen screen, int x, int y, ref int oetype, int netype)
        {
            if (oetype == netype) return;

            int px = x * 4;
            int py = y * 4;
            screen.DrawIntArray(px, py, RPsgEnv.Pixels, 128, 8 * netype, 0, 8, 8);
            DrawFont4Int2(screen, px + 12, py, netype);

            oetype = netype;
        }

        // drawBuff.cs:4518 ChYM2203_P - channel badge. FM (ch<3): number badge "1".."3".
        // FM-EX op slots (3<=ch<6): number badge "1".."3" at a different sprite-sheet
        // offset (32,0) - the ch==2/FM-EX-mode-3 case is drawn via Ch3 below instead, not
        // through this path. SSG (6<=ch<9): a plain wide icon, no number (32x8 region at
        // (32*(ch-5),24)).
        private static void ChYm2203P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            if (ch < 3)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
            }
            else if (ch < 6)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 32, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch - 3).ToString());
            }
            else if (ch < 9)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 32 * (ch - 5), 24, 32, 8);
            }
        }

        // drawBuff.cs:2226 ChYM2203 - dirty-diff wrapper. Row-y placement uses the raw
        // channel index directly (8+ch*8) for EVERY channel including the FM-EX slots
        // (3,4,5) - this does NOT line up with those channels' own keyboard/volume row
        // (8+(ch)*8 where the caller passes ch+3 for that row, i.e. rows 6/7/8) - kept
        // exactly as frmYM2203.cs does it, not "fixed"; see Ym2203Visualizer.cs's
        // ScreenDrawParams comment for the full explanation.
        public static void ChYm2203(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYm2203P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:4879 Ch3YM2612_P (reused by frmYM2203.cs's Ch3YM2203 for channel 2's
        // badge) - plain "3" badge, or (Ch3-extended-mode on) a wider "EX" badge.
        private static void Ch3P(PixelScreen screen, int x, int y, int ch, bool mask, bool ex)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            if (!ex)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (ch + 1).ToString());
            }
            else
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 24, 24, 8);
            }
        }

        // drawBuff.cs:2238 Ch3YM2203 - dirty-diff wrapper, same row-placement caveat as
        // ChYm2203 above (only ever called with ch==2 in practice).
        public static void Ch3(PixelScreen screen, int ch, ref bool? om, bool? nm, ref bool oe, bool ne)
        {
            if (om == nm && oe == ne) return;
            Ch3P(screen, 0, 8 + ch * 8, ch, nm ?? false, ne);
            om = nm;
            oe = ne;
        }
    }
}
