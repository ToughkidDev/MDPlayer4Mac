// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYM2609.cs's screenDrawParams path calls. YM2609 (a virtual "dual OPNA" used by some
// arcade/PC-98 rips - two YM2608-like FM+SSG cores sharing one register-port space, plus a
// stereo 3-band EQ and 3 extra single-channel ADPCM units) reuses almost all of YM2608's
// sprite-blit vocabulary (KeyBoardOPNA/ChYM2608/Ch3YM2608/Slot/Pan/TnOPNA/font4Hex16Bit/
// font4Int2/font4Int3/font4HexByte/font4Hex12Bit/LfoSw/LfoFrq/Nfrq/Efrq/Etype/drawNESSw are
// copied verbatim here rather than referenced cross-file, per this port's per-chip
// self-containment convention - see DrawBuffYm2608.cs for the original drawBuff.cs line
// numbers backing each of those). This file adds what YM2609 alone needs: InstOPNA2 (the
// wider 16-params-per-operator instrument table, vs YM2203/YM2608's 11-per-operator Inst/
// InstOPNA), PanType4/PanType5/PanType6 (three more encodings of the same rPan2 sprite
// sheet already exported for YMF278B's PanType2), VolumeYM2609Rhythm/PanYM2609Rhythm
// (explicit-x/y variants of YM2608's rhythm-row helpers, since YM2609's rhythm section is
// laid out in columns rather than YM2608's fixed row), font4YM2609Duty (PSG-user-wave-bank
// text readout), and WaveFormYM2609Preset/WaveFormYM2609User (the PSG custom-wavetable
// icon/graph renderers - the first chip in this port with a graphical waveform display).
//
// Same deliberate simplifications as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0, and drawFont4Int's original `t` (font-colour-variant) parameter
// is dropped since this port only ever loads rFont2's t=0 variant (see DrawBuffYm2203.cs).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYm2609
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
        public static SpriteAtlas RPan2 = null!; // rPan2_01 (PanType4/5/6's 2-tile pan icon sheet)
        public static SpriteAtlas RWavGraph = null!; // PSG user-wavetable column icon strip
        public static SpriteAtlas RPsg2 = null!; // PSG preset-waveform 32x32 icon sheet

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
            RPan2 = SpriteAtlas.Load("rPan2_01");
            RWavGraph = SpriteAtlas.Load("rWavGraph");
            RPsg2 = SpriteAtlas.Load("rPSG2");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - raw pixel x/y.
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

        // drawBuff.cs:1279 VolumeYM2609Rhythm - explicit x/y (unlike YM2608's row-fixed
        // variant), fixed L/R tint t=4 regardless of the c argument the original always
        // hardcodes to 0.
        public static void VolumeYm2609Rhythm(PixelScreen screen, int x, int y, ref int ov, int nv)
        {
            if (ov == nv) return;

            const int t = 4;

            for (int i = 0; i <= 19; i++)
            {
                VolumeP(screen, x + i * 2, y, 1 + t);
            }

            for (int i = 0; i <= nv; i++)
            {
                VolumeP(screen, x + i * 2, y, i > 17 ? 2 + t : 0 + t);
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

        // drawBuff.cs:3801 drawFont4Int - leading-zero-blanked numeric readout. `t`
        // (font-colour variant) dropped, see file header.
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

        // drawBuff.cs:3937 drawFont4Int2 (zero-padded 2-digit).
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

        // drawBuff.cs:3970 drawFont4Int3 - zero-padded 3-digit readout.
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

        // drawBuff.cs:4016 drawFont4HexByte.
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

        // drawFont4Hex16Bit/font4Hex16Bit - FM/PSG/ADPCM channels' packed freq readout.
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

        // drawFont4Hex12Bit/font4Hex12Bit - Timer A's 10-bit value, 3 hex digits.
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

        // drawBuff.cs:1494 KeyBoardOPNA - piano-key highlight + note-name/octave readout.
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

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon (used here for the EQ
        // low/mid/hi on/off switches).
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs:5137 drawTnP / :3227 TnOPNA - tone/noise-mode icon.
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
        // Frequency-divider/Frequency/Type readouts (4 independent SSG cores here, one per
        // psgPort/psgAdr entry).
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
        // readouts (2 independent LFOs here, one per FM sub-core).
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

        // drawBuff.cs:4269 drawPanP / :1855 Pan (dirty-diff wrapper).
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

        // drawBuff.cs:1966 PanYM2609Rhythm - explicit x/y (unlike YM2608's row-fixed
        // variant, since YM2609's rhythm/ADPCM-A rows are laid out per-column).
        public static void PanYm2609Rhythm(PixelScreen screen, int x, int y, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;
            DrawPanP(screen, x, y, nt);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4307 drawPanType4P / PanType4 - FM channels' 2-source pan (out of 5
        // levels each: off + 4 attenuation steps), split across 2 tiles of the rPan2 sheet.
        private static void DrawPanType4P(PixelScreen screen, int x, int y, int t)
        {
            int p = t / 5;
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
            p = t % 5;
            screen.DrawIntArray(x + 4, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
        }

        public static void PanType4(PixelScreen screen, int x, int y, ref int ot, int nt, int tp)
        {
            if (ot == nt) return;
            DrawPanType4P(screen, x, y, nt);
            ot = nt;
        }

        // drawBuff.cs:4321 drawPanType5P / PanType5 - ADPCM012's single-tile L/R pan level.
        private static void DrawPanType5P(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, t * 4, 0, 4, 8);
        }

        public static void PanType5(PixelScreen screen, int x, int y, ref int ot, int nt, int tp)
        {
            if (ot == nt) return;
            DrawPanType5P(screen, x, y, nt);
            ot = nt;
        }

        // drawBuff.cs:4332 drawPanType6P / PanType6 - ADPCM-A's packed-bitfield pan (bit 5
        // = enabled, bits 3-4 = attenuation), single tile.
        private static void DrawPanType6P(PixelScreen screen, int x, int y, int t)
        {
            int p = 0;
            if ((t & 0x20) != 0)
            {
                p = 4 - ((t & 0x18) >> 3);
            }
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
        }

        public static void PanType6(PixelScreen screen, int x, int y, ref int ot, int nt, int tp)
        {
            if (ot == nt) return;
            DrawPanType6P(screen, x, y, nt);
            ot = nt;
        }

        // drawBuff.cs:4746 ChYM2608_P (reused verbatim by YM2609 for both its FM channels
        // 0-17 and, since ch>=12 always falls into the wide-icon `else` branch, its PSG
        // channels 18-29 too - the original genuinely draws PSG channels 18-29 with the
        // same unnumbered "ADPCM-style" badge as ch==12 would, since ChYM2608_P was never
        // extended for ch values above 12; transcribed faithfully rather than "fixed").
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

        // drawBuff.cs:2351 ChYM2608 - dirty-diff wrapper, row-y placement uses the raw
        // channel index directly (8+ch*8).
        public static void ChYm2608(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYm2608P(screen, 1, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:4879 Ch3YM2612_P (reused by frmYM2609.cs's Ch3YM2608 calls for
        // channels 2 and 8's badge) - plain numbered badge, or (Ch3-extended-mode on) a
        // wider "EX" badge.
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

        // drawBuff.cs:2363 Ch3YM2608 - dirty-diff wrapper (called with ch==2 and ch==8
        // here, one per FM sub-core).
        public static void Ch3(PixelScreen screen, int ch, ref bool? om, bool? nm, ref bool oe, bool ne)
        {
            if (om == nm && oe == ne) return;
            Ch3P(screen, 1, 8 + ch * 8, ch, nm ?? false, ne);
            om = nm;
            oe = ne;
        }

        // drawBuff.cs:867 InstOPNA2 - the 16-params-per-operator instrument/operator table
        // (wider than YM2203/YM2608's 11-per-operator Inst/InstOPNA, adding D2/per-op-FB/
        // WT/ALL/PR fields), laid out on a 4-column grid by the caller (x/y already include
        // the (c%4)/(c/4) offset, unlike InstOPNA which computes that internally).
        public static void InstOpna2(PixelScreen screen, int x, int y, int[] oi, int[] ni)
        {
            int sx = x;
            int sy = y;

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (oi[i + j * 16] != ni[i + j * 16])
                    {
                        DrawFont4Int(screen, sx + i * 8 + (i > 5 ? 4 : 0), sy + j * 8, i == 5 ? 3 : 2, ni[i + j * 16]);
                        oi[i + j * 16] = ni[i + j * 16];
                    }
                }
            }

            if (oi[64] != ni[64])
            {
                DrawFont4Int(screen, sx + 4 * 9, sy - 16, 2, ni[64]);
                oi[64] = ni[64];
            }
            if (oi[65] != ni[65])
            {
                DrawFont4Int(screen, sx + 4 * 14, sy - 16, 2, ni[65]);
                oi[65] = ni[65];
            }
            if (oi[66] != ni[66])
            {
                DrawFont4Int(screen, sx + 4 * 19, sy - 16, 2, ni[66]);
                oi[66] = ni[66];
            }
        }

        // drawBuff.cs:4091 drawFont4YM2609Duty / :3585 font4YM2609Duty - PSG user-wave bank
        // readout: 0=square, 1-7=duty presets, 8=triangle, 9=saw, 10+=user wavetable.
        private static void DrawFont4YM2609Duty(PixelScreen screen, int x, int y, int t, int num)
        {
            if (num == 0)
            {
                DrawFont4(screen, x, y, t, "SQ.W ");
            }
            else if (num < 8)
            {
                DrawFont4(screen, x, y, t, $"DT{8 - num}/8");
            }
            else if (num == 8)
            {
                DrawFont4(screen, x, y, t, "TRI. ");
            }
            else if (num == 9)
            {
                DrawFont4(screen, x, y, t, "SAW  ");
            }
            else
            {
                DrawFont4(screen, x, y, t, "USER ");
            }
        }

        public static void Font4Ym2609Duty(PixelScreen screen, int x, int y, int t, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4YM2609Duty(screen, x, y, t, nn);
            on = nn;
        }

        // drawBuff.cs:2734 WaveFormYM2609Preset - one of 10 preset 32x32 icons from the
        // rPSG2 sheet (bank 0-9, before the point where a bank number selects a user wave).
        public static void WaveFormYm2609Preset(PixelScreen screen, int x, int y, ref int oi, int ni)
        {
            if (oi == ni) return;
            oi = ni;
            screen.DrawIntArray(x, y, RPsg2.Pixels, 320, ni * 32, 0, 32, 32);
        }

        // drawBuff.cs:2716 WaveFormYM2609User - the PSG custom-wavetable graph: a 64-byte
        // L/R-interleaved sample array is averaged pairwise into 32 columns of a 0-255
        // level, each bucketed into 4 stacked 8px rows via the rWavGraph column-icon strip
        // (mirroring the original's per-column dirty-diff so only changed columns redraw).
        public static void WaveFormYm2609User(PixelScreen screen, int x, int y, ref byte[]? oi, byte[]? ni)
        {
            if (ni == null) return;
            oi ??= new byte[64];

            for (int i = 0; i < 32; i++)
            {
                byte l = (byte)((ni[i * 2] + ni[i * 2 + 1]) / 2);
                if (oi[i] == l) continue;
                oi[i] = l;

                int n = l / 8;
                int m = n > 7 ? 8 : n;
                screen.DrawIntArray(x + i, y, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 15 ? 8 : (n - 8 < 0 ? 0 : n - 8);
                screen.DrawIntArray(x + i, y - 8, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 23 ? 8 : (n - 16 < 0 ? 0 : n - 16);
                screen.DrawIntArray(x + i, y - 16, RWavGraph.Pixels, 64, m, 0, 1, 8);
                m = n > 31 ? 8 : (n - 24 < 0 ? 0 : n - 24);
                screen.DrawIntArray(x + i, y - 23, RWavGraph.Pixels, 64, m + 1, 0, 1, 7);
            }
        }
    }
}
