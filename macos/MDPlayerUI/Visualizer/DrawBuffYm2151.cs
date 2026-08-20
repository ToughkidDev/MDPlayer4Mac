// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmYM2151.cs's screenDrawParams path calls: InstOPM, Pan, KeyBoardOPM, Slot, Volume,
// ChYM2151(_P), KcYM2151, KfYM2151, NeYM2151, NfrqYM2151, LfrqYM2151, AmdYM2151, PmdYM2151,
// WaveFormYM2151, LfoSyncYM2151, plus the shared drawFont8/drawFont4/drawFont4Int/
// drawFont4HexByte/drawFont4Hex12Bit/drawKbn/drawPanP/VolumeP primitives those build on.
// YM2151 (OPM) has 8 plain FM channels and no PCM channel / no Ch3-style "special mode"
// extended slots (unlike YM2612), so this port is simpler than DrawBuffYm2612.cs in that
// respect - no Ch3YM2612/Ch6YM2612-style wide/PCM badge variants, just one badge shape.
//
// Same deliberate simplification as DrawBuffSn76489.cs/DrawBuffYm2612.cs: every original
// function's `tp` (0=emulated/1=real+soundcard/2=real+no soundcard) parameter is dropped
// and hardcoded to the tp=0 sprite variant, since this port's engine never supports
// real-hardware output (see VgmEngine.cs's header comment). `mask` (channel-mute)
// parameters are kept in the function signatures even though nothing in this port ever
// sets a channel's mask to true yet (no mute-toggle UI has been wired up here) - matches
// the upstream `newParam.channels[ch].mask` simply staying at its Channel-class default of
// `false` forever, and keeps these functions ready for whenever mute UI is added.
//
// Self-contained (own SpriteAtlas fields/LoadSprites, own copies of Volume/Pan/DrawFont8/
// DrawFont4/DrawKbn) rather than reusing DrawBuffSn76489's or DrawBuffYm2612's - see
// DrawBuffSn76489.cs's header comment for the shared-sprite-sheet rationale (rVol_01/
// rKBD_01/rFont_01/02/03/rType_01/02/rPan_01/rNESDMC are genuinely the same sprite sheets
// drawBuff.cs shares across every chip window in the original app - all of them were
// already exported by the SN76489/YM2612 ports, so this file reuses those existing
// .rgba32 files rather than re-exporting them, but still loads its own copies via its own
// LoadSprites() so a VGM using only YM2151 doesn't depend on another chip's visualizer
// having run first).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffYm2151
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
        // a leading-zero-blanked 3-digit number (used for TL 0..127, and for LFRQ/AMD/PMD
        // here); k==1 draws a leading-zero-blanked... actually a single-digit number
        // (used here for NE/WAVEFORM/LFOSYNC, all 0/1..3 range values); any other k (only
        // k==2 in practice) draws a leading-zero-blanked 2-digit number (used for every
        // other operator parameter here, and for NFRQ). The original only special-cases
        // k==3 with its own 3-digit branch; k==1 and k==2 share this same "2-digit tile
        // draw" path below (k==1's caller just never has a value >9 so the tens digit tile
        // always renders as blank/zero) - this matches drawBuff.cs's drawFont4Int exactly,
        // not a simplification.
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

        // drawBuff.cs:1352 KeyBoardOPM - piano-key highlight + note-name/octave readout
        // for one FM channel row. Coordinates (33+kx for the key sprites, x=329/345 for the
        // note-name/octave text) happen to be numerically identical to
        // DrawBuffYm2612.KeyBoardOPNM's, but this is transcribed from drawBuff.cs's actual
        // KeyBoardOPM function (not shared/reused code) since the two chips' windows are
        // laid out independently in the original.
        public static void KeyBoardOPM(PixelScreen screen, int ch, ref int ot, int nt)
        {
            if (ot == nt) return;

            int y = (ch + 1) * 8;

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

            DrawFont8(screen, 82 * 4 + 1, y, 1, "   ");

            if (nt >= 0)
            {
                DrawFont8(screen, 82 * 4 + 1, y, 1, Tables.kbn[nt % 12]);
                if (nt / 12 < 10)
                {
                    DrawFont8(screen, 82 * 4 + 1 + 16, y, 1, Tables.kbo[nt / 12]);
                }
            }

            ot = nt;
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

        // drawBuff.cs:4510 ChYM2151_P - channel-number badge. Unlike YM2612's badge
        // functions there's no wide/PCM/extended-slot variant here - YM2151 only has 8
        // plain FM channels, so every channel uses the same simple "number badge" shape.
        private static void ChYM2151P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 0, 0, 16, 8);
            DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:2214 ChYM2151 - dirty-diff wrapper.
        public static void ChYM2151(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChYM2151P(screen, 1, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:789 InstOPM - the instrument/operator parameter table. Draws all 4
        // operators' 11 params (AR/DR/SR/RR/SL/TL/KS/ML/DT/DT2/AM, dirty-diffed per cell)
        // plus the 4 whole-channel params (AL/FB/AMS/PMS) above them. c selects which of
        // the 8 channel slots in the table grid (3 columns x 3 rows, 8 used) this
        // channel's block goes in. Same shape as DrawBuffYm2612.InstOPN2 but with a
        // narrower per-column stride (27*4 vs 29*4) and a `y` parameter that's pre-scaled
        // by 8 inside this function (sy = (c/3)*8*6 + 8*y) rather than already being a
        // pixel value - both transcribed exactly from their respective drawBuff.cs
        // functions, not unified, since that's how the originals differ.
        public static void InstOPM(PixelScreen screen, int x, int y, int c, int[] oi, int[] ni)
        {
            int sx = (c % 3) * 27 * 4 + x;
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

        // drawBuff.cs:3090 KcYM2151 - Key Code (note+octave raw register value) hex
        // readout. Calls the raw drawFont4HexByte primitive directly (its own dirty check)
        // rather than going through the generic Font4HexByte wrapper above - matches the
        // original exactly (it does the same thing).
        public static void KcYM2151(PixelScreen screen, int ch, ref int ok, int nk)
        {
            if (ok == nk) return;
            DrawFont4HexByte(screen, 78 * 4 + 1, ch * 8 + 8, nk);
            ok = nk;
        }

        // drawBuff.cs:3103 KfYM2151 - Key Fraction hex readout.
        public static void KfYM2151(PixelScreen screen, int ch, ref int ok, int nk)
        {
            if (ok == nk) return;
            DrawFont4HexByte(screen, 80 * 4 + 1, ch * 8 + 8, nk);
            ok = nk;
        }

        // drawBuff.cs:3116 NeYM2151 - Noise Enable (0/1) readout.
        public static void NeYM2151(PixelScreen screen, ref int one, int nne)
        {
            if (one == nne) return;
            DrawFont4Int(screen, 4 * 67 + 1, 8 * 22, 1, nne);
            one = nne;
        }

        // drawBuff.cs:3130 NfrqYM2151 - Noise Frequency (0..31) readout.
        public static void NfrqYM2151(PixelScreen screen, ref int onfrq, int nnfrq)
        {
            if (onfrq == nnfrq) return;
            DrawFont4Int(screen, 4 * 67 + 1, 8 * 23, 2, nnfrq);
            onfrq = nnfrq;
        }

        // drawBuff.cs:3144 LfrqYM2151 - hardware LFO Frequency (raw byte, 0..255) readout.
        public static void LfrqYM2151(PixelScreen screen, ref int olfrq, int nlfrq)
        {
            if (olfrq == nlfrq) return;
            DrawFont4Int(screen, 4 * 66 + 1, 8 * 24, 3, nlfrq);
            olfrq = nlfrq;
        }

        // drawBuff.cs:3158 AmdYM2151 - Amplitude Modulation Depth (0..127) readout.
        public static void AmdYM2151(PixelScreen screen, ref int oamd, int namd)
        {
            if (oamd == namd) return;
            DrawFont4Int(screen, 4 * 66 + 1, 8 * 26, 3, namd);
            oamd = namd;
        }

        // drawBuff.cs:3172 PmdYM2151 - Phase Modulation Depth (0..127) readout.
        public static void PmdYM2151(PixelScreen screen, ref int opmd, int npmd)
        {
            if (opmd == npmd) return;
            DrawFont4Int(screen, 4 * 66 + 1, 8 * 25, 3, npmd);
            opmd = npmd;
        }

        // drawBuff.cs:3186 WaveFormYM2151 - hardware LFO waveform select (0..3) readout.
        public static void WaveFormYM2151(PixelScreen screen, ref int owaveform, int nwaveform)
        {
            if (owaveform == nwaveform) return;
            DrawFont4Int(screen, 4 * 83 + 1, 8 * 24, 1, nwaveform);
            owaveform = nwaveform;
        }

        // drawBuff.cs:3200 LfoSyncYM2151 - LFO sync flag (0/1) readout.
        public static void LfoSyncYM2151(PixelScreen screen, ref int olfosync, int nlfosync)
        {
            if (olfosync == nlfosync) return;
            DrawFont4Int(screen, 4 * 83 + 1, 8 * 25, 1, nlfosync);
            olfosync = nlfosync;
        }
    }
}
