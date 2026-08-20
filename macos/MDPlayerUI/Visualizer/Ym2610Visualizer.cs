// Port of MDPlayer/MDPlayerx64/form/KB/OPN/frmYM2610.cs - the YM2610 (OPNB, the Neo Geo's
// FM+SSG+ADPCM chip) channel visualizer. Structurally almost identical to YM2608 (OPNA):
// 3 FM channels + Ch3-extended-mode slots + 3 SSG channels, but instead of YM2608's
// built-in sample-based rhythm + single ADPCM channel, YM2610 has a single ADPCM-B playback
// channel and a 6-voice ADPCM-A section - 19 channels total: FM 0-5, Ch3-ex slots 6-8,
// SSG 9-11, ADPCM-B 12, ADPCM-A 13-18 (this port's MDChipParams.YM2610.channels array
// already matches this numbering exactly, unlike YM2609's - see Ym2609Visualizer.cs for
// that contrast).
//
// Data source: reads chipRegister.fmRegisterYM2610[chipID] (int[2][0x100]) and
// chipRegister.fmKeyOnYM2610[chipID] directly, plus chipRegister.GetYM2610Volume(chipID),
// GetYM2610Ch3SlotVolume(chipID), GetYM2610RhythmVolume(chipID) (int[6][2], per-ADPCM-A-
// channel L/R), and GetYM2610AdpcmVolume(chipID) (int[2], L/R for the single ADPCM-B
// channel) - all live per-frame readouts, same "no reset needed" shape as this port's other
// chip-side *Volume getters.
//
// Deliberate simplification: same ExAll hardcoded-false simplification as YM2203/YM2608/
// YM2609 (this port never wired Setting through to the visualizer layer). `tp` hardcoded 0.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2610Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.YM2610 newParam = new();
        private readonly MDChipParams.YM2610 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYM2610.cs:224-237.
        private static readonly byte[] Md = { 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x0c << 4, 0x0e << 4, 0x0e << 4, 0x0f << 4 };
        private static readonly float[] FmDivTbl = { 6, 3, 2 };
        private static readonly float[] SsgDivTbl = { 4, 2, 1 };

        public Ym2610Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffYm2610.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYM2610");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2610.cs:239 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[][] ym2610Register = chipRegister.fmRegisterYM2610[ChipID];
            if (ym2610Register == null) return;

            int[] fmKeyYM2610 = chipRegister.fmKeyOnYM2610[ChipID];
            int[] ym2610Vol = chipRegister.GetYM2610Volume(ChipID);
            int[] ym2610Ch3SlotVol = chipRegister.GetYM2610Ch3SlotVolume(ChipID);
            int[][] ym2610Rhythm = chipRegister.GetYM2610RhythmVolume(ChipID);
            int[] ym2610AdpcmVol = chipRegister.GetYM2610AdpcmVolume(ChipID);

            bool isFmEx = (ym2610Register[0][0x27] & 0x40) > 0;
            newParam.channels[2].ex = isFmEx;

            int defaultMasterClock = 8000000;
            float ssgMul = 1.0f;
            int masterClock = defaultMasterClock;
            if (clockHz != 0)
            {
                ssgMul = clockHz / (float)defaultMasterClock;
                masterClock = (int)clockHz;
            }

            int divInd = ym2610Register[0][0x2d];
            if (divInd < 0 || divInd > 2) divInd = 0;
            float fmDiv = FmDivTbl[divInd];
            float ssgDiv = SsgDivTbl[divInd];
            ssgMul = ssgMul / ssgDiv * 4;

            newParam.timerA = ym2610Register[0][0x24] | ((ym2610Register[0][0x25] & 0x3) << 8);
            newParam.timerB = ym2610Register[0][0x26];
            newParam.rhythmTotalLevel = ym2610Register[1][0x01];
            newParam.adpcmLevel = ym2610Register[0][0x1b];

            newParam.lfoSw = (ym2610Register[0][0x22] & 0x8) != 0;
            newParam.lfoFrq = ym2610Register[0][0x22] & 0x7;

            for (int ch = 0; ch < 6; ch++)
            {
                int p = ch > 2 ? 1 : 0;
                int c = ch > 2 ? ch - 3 : ch;
                MDChipParams.Channel channel = newParam.channels[ch];

                for (int i = 0; i < 4; i++)
                {
                    int ops = i == 0 ? 0 : (i == 1 ? 8 : (i == 2 ? 4 : 12));
                    channel.inst[i * 11 + 0] = ym2610Register[p][0x50 + ops + c] & 0x1f; // AR
                    channel.inst[i * 11 + 1] = ym2610Register[p][0x60 + ops + c] & 0x1f; // DR
                    channel.inst[i * 11 + 2] = ym2610Register[p][0x70 + ops + c] & 0x1f; // SR
                    channel.inst[i * 11 + 3] = ym2610Register[p][0x80 + ops + c] & 0x0f; // RR
                    channel.inst[i * 11 + 4] = (ym2610Register[p][0x80 + ops + c] & 0xf0) >> 4; // SL
                    channel.inst[i * 11 + 5] = ym2610Register[p][0x40 + ops + c] & 0x7f; // TL
                    channel.inst[i * 11 + 6] = (ym2610Register[p][0x50 + ops + c] & 0xc0) >> 6; // KS
                    channel.inst[i * 11 + 7] = ym2610Register[p][0x30 + ops + c] & 0x0f; // ML
                    channel.inst[i * 11 + 8] = (ym2610Register[p][0x30 + ops + c] & 0x70) >> 4; // DT
                    channel.inst[i * 11 + 9] = (ym2610Register[p][0x60 + ops + c] & 0x80) >> 7; // AM
                    channel.inst[i * 11 + 10] = ym2610Register[p][0x90 + ops + c] & 0x0f; // SG
                }
                channel.inst[44] = ym2610Register[p][0xb0 + c] & 0x07; // AL
                channel.inst[45] = (ym2610Register[p][0xb0 + c] & 0x38) >> 3; // FB
                channel.inst[46] = (ym2610Register[p][0xb4 + c] & 0x38) >> 4; // AMS
                channel.inst[47] = ym2610Register[p][0xb4 + c] & 0x07; // FMS

                channel.pan = (ym2610Register[p][0xb4 + c] & 0xc0) >> 6;
                channel.slot = (byte)(fmKeyYM2610[ch] >> 4);

                int freq;
                int octav;
                int n = -1;
                if (ch != 2 || !isFmEx)
                {
                    freq = ym2610Register[p][0xa0 + c] + (ym2610Register[p][0xa4 + c] & 0x07) * 0x100;
                    octav = (ym2610Register[p][0xa4 + c] & 0x38) >> 3;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKeyYM2610[ch] & 1) != 0)
                        n = Math.Min(Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    byte con = (byte)fmKeyYM2610[ch];
                    int v = 127;
                    int m = Md[ym2610Register[p][0xb0 + c] & 7];
                    v = ((con & 0x10) != 0 && (m & 0x10) != 0 && v > (ym2610Register[p][0x40 + c] & 0x7f)) ? (ym2610Register[p][0x40 + c] & 0x7f) : v; // OP1
                    v = ((con & 0x20) != 0 && (m & 0x20) != 0 && v > (ym2610Register[p][0x44 + c] & 0x7f)) ? (ym2610Register[p][0x44 + c] & 0x7f) : v; // OP3
                    v = ((con & 0x40) != 0 && (m & 0x40) != 0 && v > (ym2610Register[p][0x48 + c] & 0x7f)) ? (ym2610Register[p][0x48 + c] & 0x7f) : v; // OP2
                    v = ((con & 0x80) != 0 && (m & 0x80) != 0 && v > (ym2610Register[p][0x4c + c] & 0x7f)) ? (ym2610Register[p][0x4c + c] & 0x7f) : v; // OP4
                    channel.volumeL = Math.Min(Math.Max((int)((127 - v) / 127.0 * ((ym2610Register[p][0xb4 + c] & 0x80) != 0 ? 1 : 0) * ym2610Vol[ch] / 80.0), 0), 19);
                    channel.volumeR = Math.Min(Math.Max((int)((127 - v) / 127.0 * ((ym2610Register[p][0xb4 + c] & 0x40) != 0 ? 1 : 0) * ym2610Vol[ch] / 80.0), 0), 19);
                }
                else
                {
                    // ExAll hardcoded false (see file header) - m is always the
                    // chip-register-computed mask.
                    int m = Md[ym2610Register[0][0xb0 + 2] & 7];
                    freq = ym2610Register[0][0xa9] + (ym2610Register[0][0xad] & 0x07) * 0x100;
                    octav = (ym2610Register[0][0xad] & 0x38) >> 3;
                    newParam.channels[2].freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    // frmYM2610.cs:349 genuinely omits the "-1" that every other note-index
                    // calc in this file has - transcribed exactly as-is, not "fixed".
                    if ((fmKeyYM2610[2] & 0x10) != 0 && (m & 0x10) != 0)
                        n = Math.Min(Math.Max(Common.searchYM2608Adpcm(ff), 0), 95);

                    int v = (m & 0x10) != 0 ? ym2610Register[p][0x40 + c] : 127;
                    newParam.channels[2].volumeL = Math.Min(Math.Max((int)((127 - v) / 127.0 * ((ym2610Register[0][0xb4 + 2] & 0x80) != 0 ? 1 : 0) * ym2610Ch3SlotVol[0] / 80.0), 0), 19);
                    newParam.channels[2].volumeR = Math.Min(Math.Max((int)((127 - v) / 127.0 * ((ym2610Register[0][0xb4 + 2] & 0x40) != 0 ? 1 : 0) * ym2610Ch3SlotVol[0] / 80.0), 0), 19);
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
                    int m = Md[ym2610Register[0][0xb0 + 2] & 7];
                    int op = ch - 5;
                    op = op == 1 ? 2 : (op == 2 ? 1 : op);

                    int freq = ym2610Register[0][0xa8 + c] + (ym2610Register[0][0xac + c] & 0x07) * 0x100;
                    int octav = (ym2610Register[0][0xac + c] & 0x38) >> 3;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    int n = -1;
                    if ((fmKeyYM2610[2] & (0x10 << (ch - 5))) != 0 && (m & (0x10 << op)) != 0)
                    {
                        float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                        ff /= 1038f;
                        n = Math.Min(Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                    }
                    channel.note = n;

                    int v = (m & (0x10 << op)) != 0 ? ym2610Register[0][0x42 + op * 4] : 127;
                    channel.volumeL = Math.Min(Math.Max((int)((127 - v) / 127.0 * ym2610Ch3SlotVol[ch - 5] / 80.0), 0), 19);
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

                bool t = (ym2610Register[0][0x07] & (0x1 << ch)) == 0;
                bool n = (ym2610Register[0][0x07] & (0x8 << ch)) == 0;
                channel.tn = (t ? 1 : 0) + (n ? 2 : 0);

                channel.volumeL = ym2610Register[0][0x08 + ch] & 0xf;
                channel.volume = (t || n) ? (ym2610Register[0][0x08 + ch] & 0xf) : 0;

                int ft = ym2610Register[0][0x00 + ch * 2];
                int ct = ym2610Register[0][0x01 + ch * 2] & 0xf;
                int tp = (ct << 8) | ft;
                channel.freq = tp;

                if (channel.volumeL == 0 && (ym2610Register[0][0x08 + ch] & 0x10) == 0)
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
                channel.ex = (ym2610Register[0][0x08 + ch] & 0x10) != 0;
            }

            newParam.nfrq = ym2610Register[0][0x06] & 0x1f;
            newParam.efrq = ym2610Register[0][0x0c] * 0x100 + ym2610Register[0][0x0b];
            newParam.etype = ym2610Register[0][0x0d] & 0xf;

            // ADPCM B (single playback channel, index 12).
            MDChipParams.Channel adpcmB = newParam.channels[12];
            adpcmB.pan = (ym2610Register[0][0x11] & 0xc0) >> 6;
            if (ym2610AdpcmVol[0] != 0)
            {
                adpcmB.volumeL = Math.Min(Math.Max(ym2610AdpcmVol[0] * ym2610Register[0][0x1b], 0), 19);
            }
            else if (adpcmB.volumeL > 0)
            {
                adpcmB.volumeL--;
            }
            if (ym2610AdpcmVol[1] != 0)
            {
                adpcmB.volumeR = Math.Min(Math.Max(ym2610AdpcmVol[1] * ym2610Register[0][0x1b], 0), 19);
            }
            else if (adpcmB.volumeR > 0)
            {
                adpcmB.volumeR--;
            }
            int delta = (ym2610Register[0][0x1a] << 8) | ym2610Register[0][0x19];
            adpcmB.freq = delta;
            float frq = delta / 9447.0f;
            adpcmB.note = (ym2610Register[0][0x10] & 0x80) != 0 ? Common.searchYM2608Adpcm(frq) : -1;
            if ((ym2610Register[0][0x11] & 0xc0) == 0)
            {
                adpcmB.note = -1;
            }

            // ADPCM A (6-voice "rhythm" section, index 13-18).
            int tl = ym2610Register[1][0x01] & 0x3f;
            for (int ch = 13; ch < 19; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];
                channel.pan = (ym2610Register[1][0x08 + ch - 13] & 0xc0) >> 6;
                channel.volumeRL = ym2610Register[1][ch - 13 + 0x08] & 0x1f;
                int il = ym2610Register[1][0x08 + ch - 13] & 0x1f;

                if (ym2610Rhythm[ch - 13][0] != 0)
                {
                    channel.volumeL = Math.Min(Math.Max(ym2610Rhythm[ch - 13][0] * tl * il / 128, 0), 19);
                }
                else if (channel.volumeL > 0)
                {
                    channel.volumeL--;
                }
                if (ym2610Rhythm[ch - 13][1] != 0)
                {
                    channel.volumeR = Math.Min(Math.Max(ym2610Rhythm[ch - 13][1] * tl * il / 128, 0), 19);
                }
                else if (channel.volumeR > 0)
                {
                    channel.volumeR--;
                }
            }
        }

        // frmYM2610.cs:494 screenDrawParams.
        public void ScreenDrawParams()
        {
            // FM - SSG
            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                if (c == 2)
                {
                    DrawBuffYm2610.Volume(screen, 272 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2610.Volume(screen, 272 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2610.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2610.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2610.InstOpna(screen, 5, 17 * 8, c, oyc.inst, nyc.inst);
                    DrawBuffYm2610.Ch3(screen, c, ref oyc.mask, nyc.mask, ref oyc.ex, nyc.ex);
                    DrawBuffYm2610.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2610.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else if (c < 6)
                {
                    DrawBuffYm2610.Volume(screen, 272 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2610.Volume(screen, 272 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2610.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2610.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2610.InstOpna(screen, 5, 17 * 8, c, oyc.inst, nyc.inst);
                    DrawBuffYm2610.ChYm2610(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2610.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2610.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else
                {
                    DrawBuffYm2610.Volume(screen, 272 + 1, 8 + (c + 3) * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2610.KeyBoard(screen, 33, 8 + (c + 3) * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2610.ChYm2610(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2610.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + (c + 3) * 8, ref oyc.freq, nyc.freq);
                }
            }

            // SSG
            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 9];
                MDChipParams.Channel nyc = newParam.channels[c + 9];

                DrawBuffYm2610.VolumeShort(screen, 280 + 1, 8 + (c + 6) * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffYm2610.KeyBoard(screen, 33, (c + 6) * 8 + 8, ref oyc.note, nyc.note);
                DrawBuffYm2610.TnOpna(screen, 6, 2, c + 6, ref oyc.tn, nyc.tn, ref oyc.tntp, 0);

                DrawBuffYm2610.ChYm2610(screen, c + 9, ref oyc.mask, nyc.mask);
                DrawBuffYm2610.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + (c + 6) * 8, ref oyc.freq, nyc.freq);
                DrawBuffYm2610.Font4HexByte(screen, 272 + 1, 8 + (c + 6) * 8, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2610.DrawNesSw(screen, 268 + 1, 8 + (c + 6) * 8, ref oyc.ex, nyc.ex);
            }

            // ADPCM B
            DrawBuffYm2610.Volume(screen, 256, 8 + 13 * 8, 1, ref oldParam.channels[12].volumeL, newParam.channels[12].volumeL);
            DrawBuffYm2610.Volume(screen, 256, 8 + 13 * 8, 2, ref oldParam.channels[12].volumeR, newParam.channels[12].volumeR);
            DrawBuffYm2610.Pan(screen, 24, 8 + 13 * 8, ref oldParam.channels[12].pan, newParam.channels[12].pan, ref oldParam.channels[12].pantp, 0);
            DrawBuffYm2610.ChYm2610(screen, 13, ref oldParam.channels[12].mask, newParam.channels[12].mask);
            DrawBuffYm2610.KeyBoard(screen, 33, 8 + 13 * 8, ref oldParam.channels[12].note, newParam.channels[12].note);
            DrawBuffYm2610.Font4Hex16Bit(screen, 1 + 4 * 78, 8 + 13 * 8, ref oldParam.channels[12].freq, newParam.channels[12].freq);
            DrawBuffYm2610.Font4HexByte(screen, 272 + 1, 8 + 13 * 8, ref oldParam.channels[12].volume, newParam.channels[12].volume);

            // ADPCM A (Rhythm)
            for (int c = 0; c < 6; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 13];
                MDChipParams.Channel nyc = newParam.channels[c + 13];

                DrawBuffYm2610.VolumeYm2610Rhythm(screen, c, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2610.VolumeYm2610Rhythm(screen, c, 2, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYm2610.PanYm2610Rhythm(screen, c, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                DrawBuffYm2610.Font4Int2(screen, c * 4 * 15 + 9, 26 * 4, ref oyc.volumeRL, nyc.volumeRL);
            }
            for (int c = 0; c < 6; c++)
                DrawBuffYm2610.ChYm2610Rhythm(screen, c, ref oldParam.channels[13 + c].mask, newParam.channels[13 + c].mask);

            DrawBuffYm2610.Font4Hex12Bit(screen, 85 * 4, 30 * 4, ref oldParam.timerA, newParam.timerA);
            DrawBuffYm2610.Font4HexByte(screen, 85 * 4, 32 * 4, ref oldParam.timerB, newParam.timerB);

            DrawBuffYm2610.LfoSw(screen, 84 * 4, 18 * 8, ref oldParam.lfoSw, newParam.lfoSw);
            DrawBuffYm2610.LfoFrq(screen, 84 * 4, 19 * 8, ref oldParam.lfoFrq, newParam.lfoFrq);

            DrawBuffYm2610.Nfrq(screen, 84, 42, ref oldParam.nfrq, newParam.nfrq);
            DrawBuffYm2610.Efrq(screen, 84, 44, ref oldParam.efrq, newParam.efrq);
            DrawBuffYm2610.Etype(screen, 84, 46, ref oldParam.etype, newParam.etype);

            DrawBuffYm2610.Font4Int2(screen, 84 * 4, 50 * 4, ref oldParam.rhythmTotalLevel, newParam.rhythmTotalLevel);
            DrawBuffYm2610.Font4Int3(screen, 84 * 4, 52 * 4, ref oldParam.adpcmLevel, newParam.adpcmLevel);

            screen.Present();
        }
    }
}
