// Port of the subset of MDPlayer/MDPlayerx64/drawBuff.cs's sprite-blit functions that
// frmAY8910.cs's screenDrawParams path calls: Volume, KeyBoardDCSG, ToneNoise, ChAY8910,
// Nfrq, Efrq, Etype, drawNESSw, font4Hex12Bit, plus the shared drawFont8/drawFont4/
// drawFont4Int/drawFont4Int2/drawKbn primitives those build on. AY8910 (PSG/SSG, used by
// MSX/ZX Spectrum/etc via the AY-3-8910 and compatible chips) has 3 tone/noise channels
// plus a chip-wide hardware envelope generator (frequency/type), no stereo pan, no
// operator table - the simplest FM-adjacent chip window ported so far.
//
// Same deliberate simplification as every other DrawBuffXxx.cs in this port: every
// original function's `tp` (0=emulated/1=real+soundcard/2=real+no soundcard) parameter is
// dropped and hardcoded to the tp=0 sprite variant (see VgmEngine.cs's header comment -
// this port's engine never supports real-hardware output). `mask` parameters are kept in
// signatures even though nothing here ever sets a channel's mask to true yet (no
// mute-toggle UI wired up) - same as every other chip's port.
//
// Self-contained (own SpriteAtlas fields/LoadSprites) rather than reusing
// DrawBuffSn76489's near-identical KeyBoardDCSG/Volume - see DrawBuffSn76489.cs's header
// comment for the shared-sprite-sheet-but-independently-loaded rationale. Two sprites are
// new to this chip and not shared with any other port so far: rPSGEnv (hardware envelope
// shape icon, single un-variant sprite - the original's own rPSGEnv is not a `[tp]`-indexed
// array, so there's only one file to load, not a tp=0/1/2 family) and rPSGMode_01/02 (the
// tone/noise mode icon - the original indexes this 6-wide as tp*2+mask, so only the tp=0
// slice's two mask states, index 0 and 1, are needed/exported here).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public static class DrawBuffAy8910
    {
        public static SpriteAtlas RVol = null!;
        public static SpriteAtlas RKbd = null!;
        public static SpriteAtlas RFont1_0 = null!; // rFont_01 (drawFont8, unmasked, t=0)
        public static SpriteAtlas RFont1_1 = null!; // rFont_02 (drawFont8, masked, t=1)
        public static SpriteAtlas RFont2_0 = null!; // rFont_03 (drawFont4 / digit glyphs)
        public static SpriteAtlas RType_0 = null!; // rType_01 (channel badge, unmasked)
        public static SpriteAtlas RType_1 = null!; // rType_02 (channel badge, masked)
        public static SpriteAtlas RPsgEnv = null!; // rPSGEnv (hw envelope shape icon)
        public static SpriteAtlas RPsgMode0 = null!; // rPSGMode_01 (tone/noise icon, tp=0/unmasked)
        public static SpriteAtlas RPsgMode1 = null!; // rPSGMode_02 (tone/noise icon, tp=0/masked)
        public static SpriteAtlas RNesDmc = null!; // rNESDMC (hw-envelope-mode on/off icon; drawNESSw's original source sheet is rNESDMC, not a PSG-specific sheet)

        public static void LoadSprites()
        {
            RVol = SpriteAtlas.Load("rVol_01");
            RKbd = SpriteAtlas.Load("rKBD_01");
            RFont1_0 = SpriteAtlas.Load("rFont_01");
            RFont1_1 = SpriteAtlas.Load("rFont_02");
            RFont2_0 = SpriteAtlas.Load("rFont_03");
            RType_0 = SpriteAtlas.Load("rType_01");
            RType_1 = SpriteAtlas.Load("rType_02");
            RPsgEnv = SpriteAtlas.Load("rPSGEnv");
            RPsgMode0 = SpriteAtlas.Load("rPSGMode_01");
            RPsgMode1 = SpriteAtlas.Load("rPSGMode_02");
            RNesDmc = SpriteAtlas.Load("rNESDMC");
        }

        // drawBuff.cs:3632 VolumeP.
        private static void VolumeP(PixelScreen screen, int x, int y, int t)
        {
            screen.DrawIntArray(x, y, RVol.Pixels, 32, 2 * t, 0, 2, 8 - (t / 4) * 4);
        }

        // drawBuff.cs:976 Volume - full LED volume-bar meter (20 columns, 0..19 range).
        // AY8910 only ever calls this with c=0 (mono - no stereo pan on this chip).
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

        // drawBuff.cs:3789 drawFont4 (text glyphs; t always 0 in every call this port
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

        // drawBuff.cs:3801 drawFont4Int - leading-zero-BLANKED numeric glyphs (used here
        // for Nfrq's noise-frequency readout, 0..31 range, k=2). See DrawBuffYm2612.cs's
        // DrawFont4Int comment for the k==3/k==2 branch shapes (identical logic, this chip
        // just never needs the k==3 3-digit path).
        public static void DrawFont4Int(PixelScreen screen, int x, int y, int k, int num)
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

        // drawBuff.cs:3937 drawFont4Int2 - like DrawFont4Int above but NEVER blanks the
        // leading digit (always draws it, even when 0) - used here only by Etype's
        // hardware-envelope-type readout (0..15 range, single digit always shown next to
        // the envelope-shape icon). This is a genuinely different original function from
        // drawFont4Int, not a duplicate - matches drawBuff.cs exactly.
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

        // drawBuff.cs:4035 drawFont4Hex12Bit / :3569 font4Hex12Bit (dirty-diff wrapper) -
        // used for the tone-period (10-bit) hex readout.
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

        // drawBuff.cs:1459 KeyBoardDCSG - piano-key highlight + note-name/octave readout
        // (same shape as SN76489's - both are simple tone-generator keyboards, no PCM/FM
        // extras - but transcribed as its own copy per this port's self-contained-per-chip
        // rule).
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

        // drawBuff.cs:3674 ToneNoiseP - one 8x8 tone/noise-mode icon tile. `ntp` mirrors
        // the original's `tp*2+mask` index (always 0=unmasked or 1=masked here, since tp is
        // always 0 - see file header).
        private static void ToneNoiseP(PixelScreen screen, int x, int y, int t, int ntp)
        {
            SpriteAtlas src = ntp == 0 ? RPsgMode0 : RPsgMode1;
            screen.DrawIntArray(x, y, src.Pixels, 32, 8 * t, 0, 8, 8);
        }

        // drawBuff.cs:2583 ToneNoise - dirty-diff wrapper. `c` is the channel index (used
        // to offset y by 8px/channel); `x`/`y` are pre-scale-by-4 grid coordinates exactly
        // like the original (frmAY8910.cs calls this with x=6,y=2, i.e. pixel coords 24,8).
        public static void ToneNoise(PixelScreen screen, int x, int y, int c, ref int ot, int nt, ref int otp, int ntp)
        {
            if (ot == nt && otp == ntp) return;

            ToneNoiseP(screen, x * 4, y * 4 + c * 8, nt, ntp);
            ot = nt;
            otp = ntp;
        }

        // drawBuff.cs:4366 ChAY8910_P - channel-number badge.
        private static void ChAy8910P(PixelScreen screen, int x, int y, int ch, bool mask)
        {
            SpriteAtlas src = mask ? RType_1 : RType_0;
            screen.DrawIntArray(x, y, src.Pixels, 128, 32, 0, 16, 8);
            DrawFont8(screen, x + 16, y, mask ? 1 : 0, (1 + ch).ToString());
        }

        // drawBuff.cs:1994 ChAY8910 - dirty-diff wrapper.
        public static void ChAy8910(PixelScreen screen, int ch, ref bool? om, bool? nm)
        {
            if (om == nm) return;
            ChAy8910P(screen, 0, 8 + ch * 8, ch, nm ?? false);
            om = nm;
        }

        // drawBuff.cs:2596 Nfrq - chip-wide hardware-envelope Frequency-divider readout
        // (raw 5-bit register value, 0..31). `x`/`y` are pre-scale-by-4 grid coordinates.
        public static void Nfrq(PixelScreen screen, int x, int y, ref int onfrq, int nnfrq)
        {
            if (onfrq == nnfrq) return;
            DrawFont4Int(screen, x * 4, y * 4, 2, nnfrq);
            onfrq = nnfrq;
        }

        // drawBuff.cs:2610 Efrq - chip-wide hardware-envelope Frequency (16-bit register
        // pair) readout, shown as a 5-digit decimal string (matches the original's
        // string.Format("{0:D5}", ...) exactly, via DrawFont4's text-glyph path).
        public static void Efrq(PixelScreen screen, int x, int y, ref int oefrq, int nefrq)
        {
            if (oefrq == nefrq) return;
            DrawFont4(screen, x * 4, y * 4, nefrq.ToString("D5"));
            oefrq = nefrq;
        }

        // drawBuff.cs:2624/4262 Etype/drawEtypeP - chip-wide hardware-envelope Type (0..15)
        // readout: an icon (one of 16 envelope-shape tiles on rPSGEnv) plus a 2-digit number
        // next to it.
        public static void Etype(PixelScreen screen, int x, int y, ref int oetype, int netype)
        {
            if (oetype == netype) return;

            int px = x * 4;
            int py = y * 4;
            screen.DrawIntArray(px, py, RPsgEnv.Pixels, 128, 8 * netype, 0, 8, 8);
            DrawFont4Int2(screen, px + 12, py, netype);

            oetype = netype;
        }

        // drawBuff.cs:3520 drawNESSw - a 4x8 on/off icon (used here for AY8910's
        // hardware-envelope-mode flag, `channel.ex`). The original draws this from
        // `rNESDMC` regardless of chip - despite the name, that sheet is a generic on/off
        // icon set shared across several chip windows (NES-family chips, AY8910, YM2151's
        // operator-slot icons), not NES-specific data.
        public static void DrawNesSw(PixelScreen screen, int x, int y, ref bool os, bool ns)
        {
            if (os == ns) return;
            screen.DrawIntArray(x, y, RNesDmc.Pixels, 64, (ns ? 1 : 0) * 4 + 32, 0, 4, 8);
            os = ns;
        }
    }
}
