// Port of MDPlayer/MDPlayerx64/form/KB/OPN/frmYM2608.cs - the YM2608 (OPNA, PC-9801-86/
// PC-98's FM+SSG+rhythm+ADPCM sound chip) channel visualizer. Extends YM2203's structure
// (3 FM + Ch3-extended-mode + 3 SSG) with a second FM register port (6 FM channels total),
// genuine per-channel stereo pan, a 6-voice sample-based rhythm section, and a single ADPCM
// playback channel - 19 channels total: FM 0-5, Ch3-extended-mode op slots 6-8, SSG 9-11,
// ADPCM 12, rhythm 13-18.
//
// Data source: reads chipRegister.fmRegisterYM2608[chipID] (int[2][0x100]) and
// chipRegister.fmKeyOnYM2608[chipID] directly, plus chipRegister.GetYM2608Volume(chipID),
// GetYM2608Ch3SlotVolume(chipID), GetYM2608RhythmVolume(chipID) (int[6][2], per-rhythm-
// channel L/R), and GetYM2608AdpcmVolume(chipID) (int[2], L/R) - all live per-frame
// readouts, same "no reset needed" shape as this port's other chip-side *Volume getters.
//
// Deliberate simplification: same ExAll hardcoded-false simplification as YM2203 (see that
// file's header for the full rationale - this port never wired Setting through to the
// visualizer layer).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2608Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private readonly int ChipID;

        private readonly MDChipParams.YM2608 newParam = new();
        private readonly MDChipParams.YM2608 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYM2608.cs:108-121.
        private static readonly byte[] Md = { 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x0c << 4, 0x0e << 4, 0x0e << 4, 0x0f << 4 };
        private static readonly float[] FmDivTbl = { 6, 3, 2 };
        private static readonly float[] SsgDivTbl = { 4, 2, 1 };

        public Ym2608Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffYm2608.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeD");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2608.cs:123 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[][] ym2608Register = chipRegister.fmRegisterYM2608[ChipID];
            if (ym2608Register == null) return;

            int[] fmKeyYM2608 = chipRegister.fmKeyOnYM2608[ChipID];
            int[] ym2608Vol = chipRegister.GetYM2608Volume(ChipID);
            int[] ym2608Ch3SlotVol = chipRegister.GetYM2608Ch3SlotVolume(ChipID);
            int[][] ym2608Rhythm = chipRegister.GetYM2608RhythmVolume(ChipID);
            int[] ym2608AdpcmVol = chipRegister.GetYM2608AdpcmVolume(ChipID);

            newParam.timerA = ym2608Register[0][0x24] | ((ym2608Register[0][0x25] & 0x3) << 8);
            newParam.timerB = ym2608Register[0][0x26];
            newParam.rhythmTotalLevel = ym2608Register[0][0x11];
            newParam.adpcmLevel = ym2608Register[1][0x0b];

            bool isFmEx = (ym2608Register[0][0x27] & 0x40) > 0;
            newParam.channels[2].ex = isFmEx;

            int defaultMasterClock = 7987200;
            float ssgMul = 1.0f;
            int masterClock = defaultMasterClock;
            if (clockHz != 0)
            {
                ssgMul = clockHz / (float)defaultMasterClock;
                masterClock = (int)clockHz;
            }

            int divInd = ym2608Register[0][0x2d];
            if (divInd < 0 || divInd > 2) divInd = 0;
            float fmDiv = FmDivTbl[divInd];

            newParam.lfoSw = (ym2608Register[0][0x22] & 0x8) != 0;
            newParam.lfoFrq = ym2608Register[0][0x22] & 0x7;

            for (int ch = 0; ch < 6; ch++)
            {
                int p = ch > 2 ? 1 : 0;
                int c = ch > 2 ? ch - 3 : ch;
                MDChipParams.Channel channel = newParam.channels[ch];

                for (int i = 0; i < 4; i++)
                {
                    int ops = i == 0 ? 0 : (i == 1 ? 8 : (i == 2 ? 4 : 12));
                    channel.inst[i * 11 + 0] = ym2608Register[p][0x50 + ops + c] & 0x1f; // AR
                    channel.inst[i * 11 + 1] = ym2608Register[p][0x60 + ops + c] & 0x1f; // DR
                    channel.inst[i * 11 + 2] = ym2608Register[p][0x70 + ops + c] & 0x1f; // SR
                    channel.inst[i * 11 + 3] = ym2608Register[p][0x80 + ops + c] & 0x0f; // RR
                    channel.inst[i * 11 + 4] = (ym2608Register[p][0x80 + ops + c] & 0xf0) >> 4; // SL
                    channel.inst[i * 11 + 5] = ym2608Register[p][0x40 + ops + c] & 0x7f; // TL
                    channel.inst[i * 11 + 6] = (ym2608Register[p][0x50 + ops + c] & 0xc0) >> 6; // KS
                    channel.inst[i * 11 + 7] = ym2608Register[p][0x30 + ops + c] & 0x0f; // ML
                    channel.inst[i * 11 + 8] = (ym2608Register[p][0x30 + ops + c] & 0x70) >> 4; // DT
                    channel.inst[i * 11 + 9] = (ym2608Register[p][0x60 + ops + c] & 0x80) >> 7; // AM
                    channel.inst[i * 11 + 10] = ym2608Register[p][0x90 + ops + c] & 0x0f; // SG
                }
                channel.inst[44] = ym2608Register[p][0xb0 + c] & 0x07; // AL
                channel.inst[45] = (ym2608Register[p][0xb0 + c] & 0x38) >> 3; // FB
                channel.inst[46] = (ym2608Register[p][0xb4 + c] & 0x38) >> 4; // AMS
                channel.inst[47] = ym2608Register[p][0xb4 + c] & 0x07; // FMS

                channel.pan = (ym2608Register[p][0xb4 + c] & 0xc0) >> 6;
                channel.slot = (byte)(fmKeyYM2608[ch] >> 4);

                int freq;
                int octav;
                int n = -1;
                if (ch != 2 || !isFmEx)
                {
                    octav = (ym2608Register[p][0xa4 + c] & 0x38) >> 3;
                    freq = ym2608Register[p][0xa0 + c] + (ym2608Register[p][0xa4 + c] & 0x07) * 0x100;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKeyYM2608[ch] & 1) != 0)
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    byte con = (byte)fmKeyYM2608[ch];
                    int v = 127;
                    int m = Md[ym2608Register[p][0xb0 + c] & 7];
                    v = ((con & 0x10) != 0 && (m & 0x10) != 0 && v > (ym2608Register[p][0x40 + c] & 0x7f)) ? (ym2608Register[p][0x40 + c] & 0x7f) : v; // OP1
                    v = ((con & 0x20) != 0 && (m & 0x20) != 0 && v > (ym2608Register[p][0x44 + c] & 0x7f)) ? (ym2608Register[p][0x44 + c] & 0x7f) : v; // OP3
                    v = ((con & 0x40) != 0 && (m & 0x40) != 0 && v > (ym2608Register[p][0x48 + c] & 0x7f)) ? (ym2608Register[p][0x48 + c] & 0x7f) : v; // OP2
                    v = ((con & 0x80) != 0 && (m & 0x80) != 0 && v > (ym2608Register[p][0x4c + c] & 0x7f)) ? (ym2608Register[p][0x4c + c] & 0x7f) : v; // OP4
                    channel.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((ym2608Register[p][0xb4 + c] & 0x80) != 0 ? 1 : 0) * ym2608Vol[ch] / 80.0), 0), 19);
                    channel.volumeR = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((ym2608Register[p][0xb4 + c] & 0x40) != 0 ? 1 : 0) * ym2608Vol[ch] / 80.0), 0), 19);
                }
                else
                {
                    // ExAll hardcoded false (see file header) - m is always the chip-
                    // register-computed mask.
                    int m = Md[ym2608Register[0][0xb0 + 2] & 7];
                    freq = ym2608Register[0][0xa9] + (ym2608Register[0][0xad] & 0x07) * 0x100;
                    octav = (ym2608Register[0][0xad] & 0x38) >> 3;
                    newParam.channels[2].freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKeyYM2608[2] & 0x10) > 0 && (m & 0x10) != 0)
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    int v = (m & 0x10) != 0 ? ym2608Register[p][0x40 + c] : 127;
                    newParam.channels[2].volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((ym2608Register[0][0xb4 + 2] & 0x80) != 0 ? 1 : 0) * ym2608Ch3SlotVol[0] / 80.0), 0), 19);
                    newParam.channels[2].volumeR = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((ym2608Register[0][0xb4 + 2] & 0x40) != 0 ? 1 : 0) * ym2608Ch3SlotVol[0] / 80.0), 0), 19);
                }
                channel.note = n;
            }

            int[] exReg = { 2, 0, -6 };
            for (int ch = 6; ch < 9; ch++) // FM EX
            {
                int c = exReg[ch - 6];
                MDChipParams.Channel channel = newParam.channels[ch];
                channel.pan = 0;

                if (isFmEx)
                {
                    // ExAll hardcoded false (see file header).
                    int m = Md[ym2608Register[0][0xb0 + 2] & 7];
                    int op = ch - 5;
                    op = op == 1 ? 2 : (op == 2 ? 1 : op);

                    int freq = ym2608Register[0][0xa8 + c] + (ym2608Register[0][0xac + c] & 0x07) * 0x100;
                    int octav = (ym2608Register[0][0xac + c] & 0x38) >> 3;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    int n = -1;
                    if ((fmKeyYM2608[2] & (0x10 << (ch - 5))) != 0 && (m & (0x10 << op)) != 0)
                    {
                        float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                        ff /= 1038f;
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                    }
                    channel.note = n;

                    int v = (m & (0x10 << op)) != 0 ? ym2608Register[0][0x42 + op * 4] : 127;
                    channel.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ym2608Ch3SlotVol[ch - 5] / 80.0), 0), 19);
                }
                else
                {
                    channel.note = -1;
                    channel.volumeL = 0;
                }
            }

            for (int ch = 0; ch < 3; ch++) // SSG
            {
                MDChipParams.Channel channel = newParam.channels[ch + 9];

                bool t = (ym2608Register[0][0x07] & (0x1 << ch)) == 0;
                bool n = (ym2608Register[0][0x07] & (0x8 << ch)) == 0;
                channel.tn = (t ? 1 : 0) + (n ? 2 : 0);

                channel.volumeL = ym2608Register[0][0x08 + ch] & 0xf;
                channel.volume = (t || n) ? (ym2608Register[0][0x08 + ch] & 0xf) : 0;

                int ft = ym2608Register[0][0x00 + ch * 2];
                int ct = ym2608Register[0][0x01 + ch * 2] & 0xf;
                int tp = (ct << 8) | ft;
                channel.freq = tp;

                if (channel.volumeL == 0 && (ym2608Register[0][0x08 + ch] & 0x10) == 0)
                {
                    channel.note = -1;
                }
                else
                {
                    if (tp == 0)
                    {
                        channel.note = -1;
                    }
                    else
                    {
                        float ftone = masterClock / (64.0f * tp) * ssgMul;
                        channel.note = Common.searchSSGNote(ftone);
                    }
                }
                channel.ex = (ym2608Register[0][0x08 + ch] & 0x10) != 0;
            }

            newParam.nfrq = ym2608Register[0][0x06] & 0x1f;
            newParam.efrq = ym2608Register[0][0x0c] * 0x100 + ym2608Register[0][0x0b];
            newParam.etype = ym2608Register[0][0x0d] & 0xf;

            // ADPCM
            newParam.channels[12].pan = (ym2608Register[1][0x01] & 0xc0) >> 6;
            newParam.channels[12].volume = ym2608Register[1][0x0b];
            newParam.channels[12].volumeL = System.Math.Min(System.Math.Max(ym2608AdpcmVol[0] / 90, 0), 15);
            newParam.channels[12].volumeR = System.Math.Min(System.Math.Max(ym2608AdpcmVol[1] / 90, 0), 15);
            int delta = (ym2608Register[1][0x0a] << 8) | ym2608Register[1][0x09];
            newParam.channels[12].freq = delta;
            float frq = delta / 9447.0f;
            newParam.channels[12].note = (ym2608Register[1][0x00] & 0x80) != 0 ? Common.searchYM2608Adpcm(frq) - 1 : -1;
            if ((ym2608Register[1][0x01] & 0xc0) == 0)
            {
                newParam.channels[12].note = -1;
            }

            for (int ch = 13; ch < 19; ch++) // RHYTHM
            {
                newParam.channels[ch].pan = (ym2608Register[0][0x18 + ch - 13] & 0xc0) >> 6;
                newParam.channels[ch].volumeL = System.Math.Min(System.Math.Max(ym2608Rhythm[ch - 13][0] / 80, 0), 19);
                newParam.channels[ch].volumeR = System.Math.Min(System.Math.Max(ym2608Rhythm[ch - 13][1] / 80, 0), 19);
                newParam.channels[ch].volumeRL = ym2608Register[0][ch - 13 + 0x18] & 0x1f;
            }
        }

        // frmYM2608.cs:343 screenDrawParams. Same FM-EX badge/row-placement caveat as
        // YM2203 (see DrawBuffYm2608.cs's ChYm2608 comment) - the badge for channels 6/7/8
        // draws at rows 6/7/8 (ChYm2608's own 8+ch*8 math) while their volume/keyboard/
        // freq readouts draw at rows 9/10/11 ((c+3)*8 with c=6,7,8), exactly as
        // frmYM2608.cs itself does it.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                if (c == 2)
                {
                    DrawBuffYm2608.Volume(screen, 272 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2608.Volume(screen, 272 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2608.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2608.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2608.InstOpna(screen, 4, 17 * 8, c, oyc.inst, nyc.inst);
                    DrawBuffYm2608.Ch3(screen, c, ref oyc.mask, nyc.mask, ref oyc.ex, nyc.ex);
                    DrawBuffYm2608.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2608.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else if (c < 6)
                {
                    DrawBuffYm2608.Volume(screen, 272 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2608.Volume(screen, 272 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2608.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2608.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2608.InstOpna(screen, 4, 17 * 8, c, oyc.inst, nyc.inst);
                    DrawBuffYm2608.ChYm2608(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2608.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2608.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else
                {
                    DrawBuffYm2608.Volume(screen, 272 + 1, 8 + (c + 3) * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2608.KeyBoard(screen, 33, 8 + (c + 3) * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2608.ChYm2608(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2608.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + (c + 3) * 8, ref oyc.freq, nyc.freq);
                }
            }

            // SSG
            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 9];
                MDChipParams.Channel nyc = newParam.channels[c + 9];

                DrawBuffYm2608.VolumeShort(screen, 280 + 1, 8 + (c + 6) * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffYm2608.KeyBoard(screen, 33, (c + 6) * 8 + 8, ref oyc.note, nyc.note);
                DrawBuffYm2608.TnOpna(screen, 6, 2, c + 6, ref oyc.tn, nyc.tn, ref oyc.tntp, 0);

                DrawBuffYm2608.ChYm2608(screen, c + 9, ref oyc.mask, nyc.mask);
                DrawBuffYm2608.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + (c + 6) * 8, ref oyc.freq, nyc.freq);
                DrawBuffYm2608.Font4HexByte(screen, 272 + 1, 8 + (c + 6) * 8, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2608.DrawNesSw(screen, 268 + 1, 8 + (c + 6) * 8, ref oyc.ex, nyc.ex);
            }

            // ADPCM
            DrawBuffYm2608.VolumeShort(screen, 280 + 1, 8 + 12 * 8, 1, ref oldParam.channels[12].volumeL, newParam.channels[12].volumeL);
            DrawBuffYm2608.VolumeShort(screen, 280 + 1, 8 + 12 * 8, 2, ref oldParam.channels[12].volumeR, newParam.channels[12].volumeR);
            DrawBuffYm2608.Pan(screen, 25, 8 + 12 * 8, ref oldParam.channels[12].pan, newParam.channels[12].pan, ref oldParam.channels[12].pantp, 0);
            DrawBuffYm2608.KeyBoard(screen, 33, 8 + 12 * 8, ref oldParam.channels[12].note, newParam.channels[12].note);
            DrawBuffYm2608.ChYm2608(screen, 12, ref oldParam.channels[12].mask, newParam.channels[12].mask);
            DrawBuffYm2608.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + 12 * 8, ref oldParam.channels[12].freq, newParam.channels[12].freq);
            DrawBuffYm2608.Font4HexByte(screen, 272 + 1, 8 + 12 * 8, ref oldParam.channels[12].volume, newParam.channels[12].volume);

            // Rhythm
            for (int c = 0; c < 6; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 13];
                MDChipParams.Channel nyc = newParam.channels[c + 13];

                DrawBuffYm2608.VolumeYm2608Rhythm(screen, c, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2608.VolumeYm2608Rhythm(screen, c, 2, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYm2608.PanYm2608Rhythm(screen, c, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                DrawBuffYm2608.Font4Int2(screen, c * 4 * 15 + 4, 28 * 4, ref oyc.volumeRL, nyc.volumeRL);
                DrawBuffYm2608.ChYm2608Rhythm(screen, c, ref oldParam.channels[13 + c].mask, newParam.channels[13 + c].mask);
            }

            DrawBuffYm2608.Font4Hex12Bit(screen, 85 * 4, 30 * 4, ref oldParam.timerA, newParam.timerA);
            DrawBuffYm2608.Font4HexByte(screen, 85 * 4, 32 * 4, ref oldParam.timerB, newParam.timerB);

            DrawBuffYm2608.LfoSw(screen, 84 * 4, 18 * 8, ref oldParam.lfoSw, newParam.lfoSw);
            DrawBuffYm2608.LfoFrq(screen, 84 * 4, 19 * 8, ref oldParam.lfoFrq, newParam.lfoFrq);

            DrawBuffYm2608.Nfrq(screen, 84, 42, ref oldParam.nfrq, newParam.nfrq);
            DrawBuffYm2608.Efrq(screen, 84, 44, ref oldParam.efrq, newParam.efrq);
            DrawBuffYm2608.Etype(screen, 84, 46, ref oldParam.etype, newParam.etype);

            DrawBuffYm2608.Font4Int2(screen, 84 * 4, 50 * 4, ref oldParam.rhythmTotalLevel, newParam.rhythmTotalLevel);
            DrawBuffYm2608.Font4Int3(screen, 84 * 4, 52 * 4, ref oldParam.adpcmLevel, newParam.adpcmLevel);

            screen.Present();
        }
    }
}
