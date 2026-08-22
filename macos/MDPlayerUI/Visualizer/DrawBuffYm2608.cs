// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYM2608.cs's screenDrawParams path calls: Volume/VolumeShort (raw-pixel-coordinate,
// same as YM2203's), VolumeYM2608Rhythm, Pan, PanYM2608Rhythm, KeyBoardOPNA (called
// "KeyBoard" here, self-contained per this port's convention), InstOPNA, ChYM2608,
// Ch3YM2608, ChYM2608Rhythm, Slot, TnOPNA, font4Hex16Bit, font4HexByte, font4Hex12Bit,
// font4Int2, font4Int3, LfoSw, LfoFrq, Nfrq, Efrq, Etype, drawNESSw, plus the shared
// drawFont8/drawFont4/drawFont4Int/drawKbn primitives those build on. YM2608 (OPNA, the
// PC-9801-86/PC-98's classic FM+SSG+rhythm+ADPCM sound chip) extends YM2203's structure
// (3 FM + Ch3-extended-mode + 3 SSG) with 6 FM channels (2 register ports, 3 channels
// each), genuine per-channel stereo pan (unlike YM2203, which never draws pan), a built-in
// 6-voice sample-based rhythm section (bass/snare/cymbal/hihat/tom/rim), and a single
// ADPCM playback channel - 19 channels total (FM 0-5, Ch3-ex 6-8, SSG 9-11, ADPCM 12,
// rhythm 13-18).
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYm2608
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
        public static SpriteAtlas RPan = null!;

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
            RPan = SpriteAtlas.Load("rPan_01");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y (see DrawBuffYm2203.cs's comment).
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

        // drawBuff.cs:1000 VolumeShort - shorter (14-segment) bar, raw pixel x/y.
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

        // drawBuff.cs:1246 VolumeYM2608Rhythm - rhythm section's LED bar, fixed row
        // (8*14), x derived from the rhythm channel index (0-5), c selects mono/L/R.
        public static void VolumeYm2608Rhythm(PixelScreen screen, int rhythmCh, int c, ref int ov, int nv)
        {
            if (ov == nv) return;

            int t = 0;
            int sy = 0;
            if (c == 1 || c == 2) { t = 4; }
            if (c == 2) { sy = 4; }
            int x = rhythmCh * 4 * 15 + 20;

            for (int i = 0; i <= 19; i++)
            {
                VolumeP(screen, x + i * 2, sy + 8 * 14, 1 + t);
            }

            for (int i = 0; i <= nv; i++)
            {
                VolumeP(screen, x + i * 2, sy + 8 * 14, i > 17 ? 2 + t : 0 + t);
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

        // drawBuff.cs:3789 drawFont4 - every call site here uses t=0 except the rhythm
        // badge letters (t genuinely varies with mask).
        public static void DrawFont4(PixelScreen screen, int x, int y, int t, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3801 drawFont4Int - leading-zero-blanked numeric readout (see
        // DrawBuffYm2203.cs's comment for the contrast with the zero-padded variants).
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

        // drawBuff.cs:3937 drawFont4Int2 (zero-padded 2-digit) - used by Etype and the
        // rhythm section's per-channel level readout.
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

        public static void Font4Int2(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int2(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3970/3545 drawFont4Int3/font4Int3 - zero-padded 3-digit readout
        // (k==3 branch is byte-for-byte identical to drawFont4Int2's k==3 branch - kept as
        // a separate helper only because the original duplicates it under a different
        // name). Used for the ADPCM level readout.
        private static void DrawFont4Int3(PixelScreen screen, int x, int y, int num)
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
        }

        public static void Font4Int3(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int3(screen, x, y, nn);
            on = nn;
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

        // drawBuff.cs's drawFont4Hex16Bit / font4Hex16Bit - FM channels' packed F-Num+block
        // readout, 4 hex digits.
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

        // drawBuff.cs's drawFont4Hex12Bit / font4Hex12Bit - Timer A's 10-bit value, 3 hex
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

        // drawBuff.cs:1494 KeyBoardOPNA - piano-key highlight + note-name/octave readout;
        // takes explicit x/y.
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

        // drawBuff.cs:945 Slot - 4 small on/off icons (one per operator).
        public static void Slot(PixelScreen screen, int x, int y, ref byte os, byte ns)
        {
            if (os == ns) return;

            screen.DrawIntArray(x + 0, y, RNesDmc.Pixels, 64, ((ns & 1) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 4, y, RNesDmc.Pixels, 64, ((ns & 2) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 8, y, RNesDmc.Pixels, 64, ((ns & 4) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 12, y, RNesDmc.Pixels, 64, ((ns & 8) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);

            os = ns;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon (SSG hardware-envelope
        // flag here).
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:5137 drawTnP / :3227 TnOPNA - tone/noise-mode icon (see
        // DrawBuffYm2203.cs's Tn comment for why only rPSGMode_01 is ever loaded here too -
        // frmYM2608.cs's TnOPNA call also never adds a mask term).
        private static void DrawTnP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPsgMode0.Pixels, 32, 8 * t, 0, 8, 8);
        }

        public static void TnOpna(PixelScreen screen, int x, int y, int c, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;
            DrawTnP(screen, x * 4 + 1, y * 4 + c * 8, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:2596/2610/2624 Nfrq/Efrq/Etype - chip-wide hardware-envelope
        // Frequency-divider/Frequency/Type readouts (same SSG core as AY8910/S5B).
        public static void Nfrq(PixelScreen screen, int x, int y, ref int onfrq, int nnfrq)
        {
            if (onfrq == nnfrq) return;
            DrawFont4Int(screen, x * 4, y * 4, 2, nnfrq);
            onfrq = nnfrq;
        }

        public static void Efrq(PixelScreen screen, int x, int y, ref int oefrq, int nefrq)
        {
            if (oefrq == nefrq) return;
            DrawFont4(screen, x * 4, y * 4, 0, nefrq.ToString("D5"));
            oefrq = nefrq;
        }

        public static void Etype(PixelScreen screen, int x, int y, ref int oetype, int netype)
        {
            if (oetype == netype) return;

            int px = x * 4;
            int py = y * 4;
            screen.DrawIntArray(px, py, RPsgEnv.Pixels, 128, 8 * netype, 0, 8, 8);
            DrawFont4Int2(screen, px + 12, py, netype);

            oetype = netype;
        }

        // drawBuff.cs:3299/3311 LfoSw/LfoFrq - hardware LFO on/off + frequency (0..7)
        // readouts.
        public static void LfoSw(PixelScreen screen, int x, int y, ref bool olfosw, bool nlfosw)
        {
            if (olfosw == nlfosw) return;
            DrawFont4(screen, x, y, 0, nlfosw ? "ON " : "OFF");
            olfosw = nlfosw;
        }

        public static void LfoFrq(PixelScreen screen, int x, int y, ref int olfofrq, int nlfofrq)
        {
            if (olfofrq == nlfofrq) return;
            DrawFont4Int(screen, x, y, 1, nlfofrq);
            olfofrq = nlfofrq;
        }

        // drawBuff.cs:4269 drawPanP / :1855 Pan (dirty-diff wrapper). `ntp` mirrors the
        // original's tp parameter (always 0 here, see file header).
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

        // drawBuff.cs:1953 PanYM2608Rhythm - rhythm section's per-channel pan icon, fixed
        // row (8*14), x derived from the rhythm channel index.
        public static void PanYm2608Rhythm(PixelScreen screen, int rhythmCh, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;
            DrawPanP(screen, rhythmCh * 4 * 15 + 12, 8 * 14, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4746 ChYM2608_P - channel badge. FM (ch<6): number badge "1".."6".
        // FM-EX op slots (6<=ch<9): number badge "1".."3" at a different sprite-sheet
        // offset. SSG (9<=ch<12): wide icon, no number. ADPCM (ch==12): another fixed-
        // offset wide icon, no number.
        private static void ChYm2608P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            if (ch < 6)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
            }
            else if (ch < 9)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 32, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch - 6).ToString());
            }
            else if (ch < 12)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 32 * (ch - 8), 24, 32, 8);
            }
            else
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 64, 0, 24, 8);
            }
        }

        // Places a channel badge at a visual row independently of the source channel
        // number.  This keeps the OPN channel view grouped as FM -> SSG -> PCM.
        public static void ChYm2608At(PixelScreen screen, int row, int ch, ref bool? om, bool? nm)
        {
            bool mask = nm ?? false;
            if (om.HasValue && om.Value == mask) return;
            ChYm2608P(screen, 1, 8 + row * 8, ch, mask);
            om = mask;
        }

        // drawBuff.cs:2351 ChYM2608 - original source-channel placement.
        public static void ChYm2608(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            ChYm2608At(screen, ch, ch, ref om, nm);
        }

        // drawBuff.cs:4879 Ch3YM2612_P (reused by frmYM2608.cs's Ch3YM2608 for channel 2's
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

        // drawBuff.cs:2363 Ch3YM2608 - dirty-diff wrapper (only ever called with ch==2).
        public static void Ch3(PixelScreen screen, int ch, ref bool? om, bool? nm, ref bool oe, bool ne)
        {
            if (om == nm && oe == ne) return;
            Ch3P(screen, 1, 8 + ch * 8, ch, nm ?? false, ne);
            om = nm;
            oe = ne;
        }

        // drawBuff.cs:4770 ChYM2608Rhythm_P - single-letter rhythm-channel label
        // (B/S/C/H/T/R), fixed row, x derived from the rhythm channel index.
        private static void ChYm2608RhythmP(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            int t = mask ? 1 : 0;
            switch (ch)
            {
                case 0: DrawFont4(screen, x + 0 * 4, y, t, "B"); break;
                case 1: DrawFont4(screen, x + 15 * 4, y, t, "S"); break;
                case 2: DrawFont4(screen, x + 30 * 4, y, t, "C"); break;
                case 3: DrawFont4(screen, x + 45 * 4, y, t, "H"); break;
                case 4: DrawFont4(screen, x + 60 * 4, y, t, "T"); break;
                case 5: DrawFont4(screen, x + 75 * 4, y, t, "R"); break;
            }
        }

        // drawBuff.cs:2376 ChYM2608Rhythm - dirty-diff wrapper, fixed row (8*14).
        public static void ChYm2608Rhythm(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYm2608RhythmP(screen, 0, 8 * 14, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:828 InstOPNA - the instrument/operator parameter table (see
        // DrawBuffYm2203.cs's Inst comment - identical shape, different sx/sy scale: x is
        // used directly here rather than x*8, and y is used directly rather than y*8).
        public static void InstOpna(PixelScreen screen, int x, int y, int c, int[] oi, int[] ni)
        {
            int sx = (c % 3) * 4 * 25 + x;
            int sy = (c / 3) * 8 * 6 + y;

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
    }
}
