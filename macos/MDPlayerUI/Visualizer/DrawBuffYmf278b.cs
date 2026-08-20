// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYMF278B.cs's screenDrawParams path calls: SUSFlag, font4Int1, font4Int2 (both the
// 2-digit and 3-digit variants - YMF278B is the first chip in this port whose readouts need
// 3 digits, for the PCM section's TL/Wav fields), font4Hex12Bit, Pan, PanType2, KeyBoard,
// KeyBoardToYMF278BPCM, VolumeXY, ChYMF278B, drawNESSw, Kakko, plus the shared
// drawFont8/drawFont4/drawKbn primitives those build on. YMF278B (OPL4, the successor to
// OPL3 with a built-in 24-channel wavetable/PCM section - MSX-AUDIO/OPL4 cards, some
// arcade boards) reuses the exact same 18 FM + 5 rhythm channel structure as YMF262
// (2 register ports for FM, ConnectSelect 4-op pairing, genuine stereo pan, ports 0/1),
// plus an entirely new 24-channel PCM section (port 2) with its own ADSR envelope,
// vibrato/LFO, reverb, and a different (15-octave) keyboard range/pan encoding.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: `tp` is
// dropped/hardcoded to 0. Keeps DrawFont4's real masked/unmasked `t` parameter like the
// other OPL-family files, for the rhythm-row-label reason.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYmf278b
    {
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4, unmasked, t=0)
        public static SpriteAtlas RFont2_1 = null!; // rFont_04 (drawFont4, masked, t=1)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RNesDmc = null!;
        public static SpriteAtlas RPan = null!;
        public static SpriteAtlas RPan2 = null!;
        public static SpriteAtlas RKakko = null!;

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
            RNesDmc = SpriteAtlas.Load("rNESDMC");
            RPan = SpriteAtlas.Load("rPan_01");
            RPan2 = SpriteAtlas.Load("rPan2_01");
            RKakko = SpriteAtlas.Load("rKakko_00");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:1171 VolumeXY - see DrawBuffYmf262.cs's comment for the `c` parameter.
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

        // drawBuff.cs:3789 drawFont4 - real t parameter kept.
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

        // drawBuff.cs:3928 drawFont4Int1 - single-digit (mod 10) readout. Every call site in
        // frmYMF278B.cs passes a literal t=0.
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

        // drawBuff.cs:3937 drawFont4Int2 - never blanks the leading digit. Every FM
        // operator-table call site in frmYMF278B.cs passes a literal t=0, k=0 (2-digit).
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

        // drawBuff.cs:3537 font4Int2 - dirty-diff wrapper (2-digit).
        public static void Font4Int2(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int2(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:3937 drawFont4Int2's k==3 branch - 3-digit readout, used only for the
        // PCM section's TL (inst[11]) and Wav (inst[12]) fields (frmYMF278B.cs:567-568 pass
        // literal k=3).
        private static void DrawFont4Int2ThreeDigit(PixelScreen screen, int x, int y, int num)
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

        public static void Font4Int2ThreeDigit(PixelScreen screen, int x, int y, ref int on, int nn)
        {
            if (on == nn) return;
            DrawFont4Int2ThreeDigit(screen, x, y, nn);
            on = nn;
        }

        // drawBuff.cs:4035 drawFont4Hex12Bit / :3569 font4Hex12Bit.
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

        // drawBuff.cs:3264 SUSFlag - single-character "-"/"*" flag readout. frmYMF278B.cs
        // always calls this with a literal t=1 (same as YMF262).
        public static void SusFlag(PixelScreen screen, int x, int y, int t, ref int oi, int ni)
        {
            if (oi == ni) return;
            DrawFont4(screen, x * 4, y * 4, t, ni == 0 ? "-" : "*");
            oi = ni;
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

        // drawBuff.cs:4275 drawPanType2P / :1868 PanType2 - byte-packed L/R pan readout used
        // by the PCM section (nyc.pan low nibble = R weight, high nibble = L weight, from
        // frmYMF278B.cs:387-392's 0-15 pan-register remap). Two 4x8 icon halves.
        private static void DrawPanType2P(PixelScreen screen, int x, int y, int t)
        {
            int p = t & 0x0f;
            p = p == 0 ? 0 : (1 + p / 4);
            screen.DrawIntArray(x, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);

            p = (t & 0xf0) >> 4;
            p = p == 0 ? 0 : (1 + p / 4);
            screen.DrawIntArray(x + 4, y, RPan2.Pixels, 32, p * 4, 0, 4, 8);
        }

        // drawBuff.cs:1868 PanType2(screen, c, ref ot, nt, tp) - fixed x=24, y=8+c*8 (same
        // x as the FM channels' plain Pan/RPan readout). frmYMF278B.cs:555 calls this with
        // c=(pcmChannelIndex-4) for the PCM section's pan readout.
        public static void PanType2(PixelScreen screen, int c, ref int ot, int nt)
        {
            if (ot == nt) return;
            DrawPanType2P(screen, 24, 8 + c * 8, nt);
            ot = nt;
        }

        // drawBuff.cs:1315 KeyBoard - piano-key highlight + note-name/octave readout (FM
        // section, 8-octave range).
        public static void KeyBoard(PixelScreen screen, int c, ref int ot, int nt)
        {
            if (ot == nt) return;

            int y = (c + 1) * 8;

            if (ot >= 0 && ot < 12 * 8)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, 32 + kx, y, kt);
            }

            if (nt >= 0 && nt < 12 * 8)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, 32 + kx, y, kt);
            }

            DrawFont8(screen, 296, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 296, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 312, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:1745 KeyBoardToYMF278BPCM - PCM section's keyboard, 15-octave range
        // (vs FM's 8), note-name readout drawn further right (300+8*24) to clear the wider
        // keyboard.
        public static void KeyBoardToYmf278bPcm(PixelScreen screen, int y, ref int ot, int nt)
        {
            if (ot == nt) return;

            y = (y + 1) * 8;

            if (ot >= 0 && ot < 12 * 15)
            {
                int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                int kt = Tables.kbl[(ot % 12) * 2 + 1];
                DrawKbn(screen, 32 + kx, y, kt);
            }

            if (nt >= 0 && nt < 12 * 15)
            {
                int kx = Tables.kbl[(nt % 12) * 2] + nt / 12 * 28;
                int kt = Tables.kbl[(nt % 12) * 2 + 1] + 4;
                DrawKbn(screen, 32 + kx, y, kt);
            }

            DrawFont8(screen, 300 + 8 * 24, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 300 + 8 * 24, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 15)
                {
                    DrawFont8(screen, 300 + 8 * 26, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
        }

        // drawBuff.cs:3520 drawNESSw - generic 4x8 on/off icon (ConnectSelect flags, DA/DV).
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }

        // drawBuff.cs's Kakko - variable-height bracket, see DrawBuffYmf262.cs's comment.
        public static void Kakko(PixelScreen screen, int x, int y, int t, ref int ot, int nt)
        {
            if (ot == nt) return;

            screen.DrawIntArray(x, y, RKakko.Pixels, 16, nt * 4, 0, 4, 8);
            for (int n = 0; n < t; n++)
            {
                screen.DrawIntArray(x, y + n * 8 + 8, RKakko.Pixels, 16, nt * 4, 8, 4, 8);
            }
            screen.DrawIntArray(x, y + t * 8 + 8, RKakko.Pixels, 16, nt * 4, 16, 4, 8);

            ot = nt;
        }

        // drawBuff.cs:2328 YMF278BCh - remaps a raw channel index to the badge number/switch
        // value ChYMF278B_P uses: FM channels (0-17) go through the same chTbl-derived
        // reorder as YMF262 (paired 9-17 offset copy of the 0-8 pattern); rhythm (18-22) and
        // PCM (23-46) are identity.
        private static readonly byte[] Ymf278bCh =
        {
            0, 3, 1, 4, 2, 5, 6, 7, 8, 9, 12, 10, 13, 11, 14, 15, 16, 17,
            18, 19, 20, 21, 22,
            23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
            35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46,
        };

        // drawBuff.cs:4708 ChYMF278B_P - channel badge. FM (ch<18): 2-digit number badge
        // (Ymf278bCh-remapped). Rhythm (18<=ch<23): plain abbreviation. PCM (23<=ch<47):
        // 2-digit number badge using a second badge-icon variant (offset 16 into the rType
        // sheet) and (ch-23+1) as the number - i.e. PCM channels are numbered 1-24
        // independently of the FM channel numbering, since Ymf278bCh is identity there.
        private static void ChYmf278bP(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            int t = mask ? 1 : 0;

            if (ch < 18)
            {
                SpriteAtlas src = mask ? RType_1 : RType_0;
                screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
                DrawFont4(screen, x + 16, y, t, (1 + ch).ToString("d2"));
                return;
            }

            if (ch < 23)
            {
                switch (ch)
                {
                    case 18: DrawFont4(screen, (ch - 18) * 4 * 13 + 10 * 4, y, t, "BD"); break;
                    case 19: DrawFont4(screen, (ch - 18) * 4 * 13 + 10 * 4, y, t, "SD"); break;
                    case 20: DrawFont4(screen, (ch - 18) * 4 * 13 + 10 * 4, y, t, "TM"); break;
                    case 21: DrawFont4(screen, (ch - 18) * 4 * 13 + 9 * 4, y, t, "CYM"); break; // 3 characters
                    case 22: DrawFont4(screen, (ch - 18) * 4 * 13 + 10 * 4, y, t, "HH"); break;
                }
                return;
            }

            SpriteAtlas src2 = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src2.Pixels, 128, 16, 0, 16, 8);
            int pcmCh = ch - 23;
            DrawFont4(screen, x + 16, y, t, (1 + pcmCh).ToString("d2"));
        }

        // drawBuff.cs:2335 ChYMF278B - dirty-diff wrapper. Row-y placement: FM channels use
        // ch*8 (rows 0-17), rhythm channels 18-22 share row 18 (the badge x-position, not y,
        // distinguishes them - handled inside ChYmf278bP), PCM channels 23-46 use
        // (ch-4)*8 = rows 19-42 (matching frmYMF278B.cs's own row math exactly).
        public static void ChYmf278b(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            int y = ch < 18 ? 8 + ch * 8 : (ch < 23 ? 8 + 18 * 8 : 8 + (ch - 4) * 8);
            ChYmf278bP(screen, 0, y, Ymf278bCh[ch], nm ?? false);
            om = nm;
        }
    }
}
