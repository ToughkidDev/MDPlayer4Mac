// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYM2612.cs's screenDrawParams path calls, for the 6 normal FM channels + the 3 Ch3
// "special mode" extended slots + the plain (non-XGM/XGM2) PCM channel 6 display + the
// instrument/operator parameter table + LFO/timer readouts. Same deliberate simplification
// as DrawBuffSn76489.cs: every original function's `tp` (0=emulated/1=real+soundcard/
// 2=real+no soundcard) parameter is dropped and hardcoded to the tp=0 sprite variant,
// since this port's engine never supports real-hardware output (see VgmEngine.cs's header
// comment). `mask` (channel-mute) parameters are kept in the function signatures even
// though nothing in this port ever sets a channel's mask to true yet (no mute-toggle UI
// has been wired up here) - this matches the upstream `newParam.channels[ch].mask` simply
// staying at its Channel-class default of `false` forever, and keeps these functions ready
// for whenever mute UI is added.
//
// Self-contained (own SpriteAtlas fields/LoadSprites, own copies of Volume/Pan/DrawFont8/
// DrawFont4/DrawKbn) rather than reusing DrawBuffSn76489's - see that file's identical
// primitives for the shared-sprite-sheet rationale (rVol_01/rKBD_01/rFont_01/02/03/
// rType_01/02/rPan_01 are genuinely the same sprite sheets drawBuff.cs shares across every
// chip window in the original app). Duplicating instead of cross-referencing means a VGM
// with only YM2612 (no SN76489) doesn't depend on Sn76489Visualizer ever having run its
// LoadSprites() first - each chip visualizer loads and owns its own copies independently,
// matching the "self-contained port file" pattern DrawBuffSn76489.cs established.
//
// XGM/XGM2-specific PCM-channel display (Ch6YM2612XGM/Ch6YM2612XGM2, the 4/3-slot sample
// player UI for those two PCM driver formats) is out of scope for this round - VGM/VGZ
// files (the only format this port's UI file picker realistically feeds it today) always
// take frmYM2612.cs's plain "else" branch for channel 6, so that's the only PCM-channel
// path ported here.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYm2612
    {
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4 / digit glyphs)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RPan = null!; // rPan_01
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (operator-slot on/off icons)

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
            RNesDmc = SpriteAtlas.Load("rNESDMC");
        }

        // drawBuff.cs:3632 VolumeP.
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

        // drawBuff.cs:3638 drawKbn.
        private static void DrawKbn(PixelScreen screen, int x, int y, int t)
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

        // drawBuff.cs:3789 drawFont4 (text glyphs; t is always 0 in every call this port
        // needs, see file header).
        public static void DrawFont4(PixelScreen screen, int x, int y, string msg)
        {
            foreach (char c in msg)
            {
                int cd = c - 'A' + 0x20 + 1;
                screen.DrawIntArray(x, y, RFont2_0.Pixels, 128, (cd % 32) * 4, (cd / 32) * 8, 4, 8);
                x += 4;
            }
        }

        // drawBuff.cs:3801 drawFont4Int (numeric-only glyphs, a different tile region of
        // the same rFont_03 sheet than drawFont4's text glyphs - offset x=64+). k==3 draws
        // a leading-zero-blanked 3-digit number (used for TL, 0..127); any other k draws a
        // leading-zero-blanked 2-digit number (used for every other 2-hex-digit-range
        // operator parameter, and reused as-is for LfoFrq's single-digit 0..7 value - the
        // original only special-cases k==3, so k==1/k==2 share this same 2-digit path).
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

        // drawBuff.cs:4035 drawFont4Hex12Bit / :3569 font4Hex12Bit (dirty-diff wrapper) -
        // used for the 10-bit Timer A readout (3 hex digits).
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

        public static void Font4Hex12Bit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Hex12Bit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:4060 drawFont4Hex16Bit / :3577 font4Hex16Bit (dirty-diff wrapper) -
        // used for the FM channel frequency+octave readout (11-bit freq | 3-bit octave
        // packed into up to 16 bits).
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

        // drawBuff.cs:4269 drawPanP.
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

        // drawBuff.cs:1529 KeyBoardOPNM - piano-key highlight + note-name/octave readout
        // for one FM channel row. Same shape as DrawBuffSn76489.KeyBoardDCSG, just
        // different x-offsets (this chip's plane background places the keyboard grid at a
        // different column) and an extra "+1 row" y adjustment matching the original.
        public static void KeyBoardOPNM(PixelScreen screen, int y, ref int ot, int nt)
        {
            if (ot == nt) return;

            y = (y + 1) * 8;

            if (ot >= 0 && ot < 12 * 8)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, 33 + kx, y, kt);
            }

            if (nt >= 0 && nt < 12 * 8)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, 33 + kx, y, kt);
            }

            DrawFont8(screen, 329, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 329, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 345, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:4861 ChYM2612_P - channel-number badge for normal FM channels
        // (ch<5) and the wider "extended slot" badge region (5<=ch<10, ch==5/PCM is
        // skipped - Ch6YM2612 owns that badge instead).
        private static void ChYM2612P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            if (ch == 5) return;

            SpriteAtlas src = mask ? RType_1 : RType_0;
            if (ch < 5)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, (ch + 1).ToString());
            }
            else if (ch < 10)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 32 * (ch - 5), 24, 32, 8);
            }
        }

        // drawBuff.cs:2425 ChYM2612 - dirty-diff wrapper.
        public static void ChYM2612(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYM2612P(screen, 1, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:4879 Ch3YM2612_P - ch3's badge, either the plain "3" badge or (when
        // Ch3 special/extended mode is on) a wider "EX" badge covering the 3 extra slot
        // rows this mode exposes.
        private static void Ch3YM2612P(PixelScreen screen, int x, int y, int ch, bool mask, bool ex)
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

        // drawBuff.cs:2437 Ch3YM2612 - dirty-diff wrapper.
        public static void Ch3YM2612(PixelScreen screen, int ch, ref bool? om, bool? nm, ref bool oe, bool ne)
        {
            if (om == nm && oe == ne) return;
            Ch3YM2612P(screen, 1, 8 + ch * 8, ch, nm ?? false, ne);
            om = nm;
            oe = ne;
        }

        // drawBuff.cs:4847 Ch6YM2612_P - channel 6's badge, either the plain "6" badge
        // (FM mode) or a PCM-mode badge (m!=0).
        private static void Ch6YM2612P(PixelScreen screen, int x, int y, int m, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            if (m == 0)
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont8(screen, x + 16, y, mask ? 1 : 0, "6");
            }
            else
            {
                screen.DrawIntArray(x, y, src.Pixels, 128, 16, 0, 16, 8);
                DrawFont8(screen, x + 16, y, 0, " ");
            }
        }

        // drawBuff.cs:2450 Ch6YM2612 - dirty-diff wrapper (plain/non-XGM path; `tp`
        // tracking dropped since it's always 0 here, see file header - only `buff`/pcmMode/
        // mask are compared).
        public static void Ch6YM2612(PixelScreen screen, int buff, ref int ot, int nt, ref bool? om, bool? nm)
        {
            if (buff == 0 && ot == nt && om == nm) return;
            Ch6YM2612P(screen, 1, 48, nt, nm ?? false);
            ot = nt;
            om = nm;
        }

        // drawBuff.cs:711 InstOPN2 - the instrument/operator parameter table. Draws all 4
        // operators' 11 params (AR/DR/SR/RR/SL/TL/KS/ML/DT/AM/SG, dirty-diffed per cell)
        // plus the 4 whole-channel params (AL/FB/AMS/FMS) above them. c selects which of
        // the up to 9 channel slots in the table grid this channel's block goes in (3
        // columns x N rows).
        public static void InstOPN2(PixelScreen screen, int x, int y, int c, int[] oi, int[] ni)
        {
            int sx = (c % 3) * 4 * 29 + x;
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

        // drawBuff.cs:945 Slot - 4 small on/off icons (one per operator) showing which
        // operators are currently gated on (from the raw keyon nibble).
        public static void Slot(PixelScreen screen, int x, int y, ref byte os, byte ns)
        {
            if (os == ns) return;

            screen.DrawIntArray(x + 0, y, RNesDmc.Pixels, 64, ((ns & 1) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 4, y, RNesDmc.Pixels, 64, ((ns & 2) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 8, y, RNesDmc.Pixels, 64, ((ns & 4) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);
            screen.DrawIntArray(x + 12, y, RNesDmc.Pixels, 64, ((ns & 8) != 0 ? 1 : 0) * 4 + 32, 0, 4, 8);

            os = ns;
        }

        // drawBuff.cs:3299 LfoSw - "ON "/"OFF" text.
        public static void LfoSw(PixelScreen screen, int x, int y, ref bool olfosw, bool nlfosw)
        {
            if (olfosw == nlfosw) return;
            DrawFont4(screen, x, y, nlfosw ? "ON " : "OFF");
            olfosw = nlfosw;
        }

        // drawBuff.cs:3311 LfoFrq - single-digit (0..7) LFO frequency readout.
        public static void LfoFrq(PixelScreen screen, int x, int y, ref int olfofrq, int nlfofrq)
        {
            if (olfofrq == nlfofrq) return;
            DrawFont4Int(screen, x, y, 1, nlfofrq);
            olfofrq = nlfofrq;
        }
    }
}
