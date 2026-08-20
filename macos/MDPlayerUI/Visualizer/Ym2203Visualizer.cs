// Port of MDPlayer/MDPlayerx64/form/KB/OPN/frmYM2203.cs - the YM2203 (OPN, PC-8801/9801's
// FM+SSG sound chip) channel visualizer: 3 FM channels (full 4-operator table each) + a
// "Ch3 extended mode" that splits FM channel 3 into 3 independently-tunable operator slots
// (shown as 3 extra rows, channels 3-5 in this port's Channel array) + 3 SSG tone/noise
// channels (channels 6-8) sharing the same hardware-envelope core as AY8910/S5B.
//
// Data source: reads chipRegister.fmRegisterYM2203[chipID] directly, plus
// chipRegister.fmKeyOnYM2203[chipID] (KeyOn), chipRegister.GetYM2203Volume(chipID) and
// chipRegister.GetYM2203Ch3SlotVolume(chipID) (both live per-frame FM volume readouts,
// same "read fresh every frame, no reset needed" shape as this port's other *Volume
// getters).
//
// Deliberate simplification: the original also reads parent.setting.other.ExAll (a user
// preference toggling whether Ch3-extended-mode's per-slot volume always shows "fully
// audible" regardless of the chip's own per-operator mute mask) to override its computed
// `m` mask with 0xf0. This port has never wired the Setting UI through to the visualizer
// layer (see the YM2151 hosei/YMF278B MoonDriver-key-on simplifications for the same
// pattern elsewhere in this port), so ExAll is hardcoded to its original default value of
// false - the `m` mask is always the chip-register-computed one, correct for every session
// this port actually runs (no settings screen exists to change it from false anyway).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2203Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private readonly int ChipID;

        private readonly MDChipParams.YM2203 newParam = new();
        private readonly MDChipParams.YM2203 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYM2203.cs:93-106.
        private static readonly byte[] Md = { 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x0c << 4, 0x0e << 4, 0x0e << 4, 0x0f << 4 };
        private static readonly float[] FmDivTbl = { 6, 3, 2 };
        private static readonly float[] SsgDivTbl = { 4, 2, 1 };

        public Ym2203Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffYm2203.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYM2203");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2203.cs:108 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] ym2203Register = chipRegister.fmRegisterYM2203[ChipID];
            if (ym2203Register == null) return;

            int[] fmKeyYM2203 = chipRegister.fmKeyOnYM2203[ChipID];
            int[] ym2203Vol = chipRegister.GetYM2203Volume(ChipID);
            int[] ym2203Ch3SlotVol = chipRegister.GetYM2203Ch3SlotVolume(ChipID);

            bool isFmEx = (ym2203Register[0x27] & 0x40) > 0;
            newParam.channels[2].ex = isFmEx;

            int defaultMasterClock = 7987200 / 2;
            double ssgMul = 1.0;
            int masterClock = defaultMasterClock;
            if (clockHz != 0)
            {
                ssgMul = clockHz / (float)defaultMasterClock;
                masterClock = (int)clockHz;
            }

            int divInd = ym2203Register[0x2d];
            if (divInd < 0 || divInd > 2) divInd = 0;
            float fmDiv = FmDivTbl[divInd];
            float ssgDiv = SsgDivTbl[divInd];
            ssgMul = ssgMul / ssgDiv * 4;

            for (int ch = 0; ch < 3; ch++)
            {
                int c = ch;
                MDChipParams.Channel channel = newParam.channels[ch];

                for (int i = 0; i < 4; i++)
                {
                    int ops = i == 0 ? 0 : (i == 1 ? 8 : (i == 2 ? 4 : 12));
                    channel.inst[i * 11 + 0] = ym2203Register[0x50 + ops + c] & 0x1f; // AR
                    channel.inst[i * 11 + 1] = ym2203Register[0x60 + ops + c] & 0x1f; // DR
                    channel.inst[i * 11 + 2] = ym2203Register[0x70 + ops + c] & 0x1f; // SR
                    channel.inst[i * 11 + 3] = ym2203Register[0x80 + ops + c] & 0x0f; // RR
                    channel.inst[i * 11 + 4] = (ym2203Register[0x80 + ops + c] & 0xf0) >> 4; // SL
                    channel.inst[i * 11 + 5] = ym2203Register[0x40 + ops + c] & 0x7f; // TL
                    channel.inst[i * 11 + 6] = (ym2203Register[0x50 + ops + c] & 0xc0) >> 6; // KS
                    channel.inst[i * 11 + 7] = ym2203Register[0x30 + ops + c] & 0x0f; // ML
                    channel.inst[i * 11 + 8] = (ym2203Register[0x30 + ops + c] & 0x70) >> 4; // DT
                    channel.inst[i * 11 + 9] = (ym2203Register[0x60 + ops + c] & 0x80) >> 7; // AM
                    channel.inst[i * 11 + 10] = ym2203Register[0x90 + ops + c] & 0x0f; // SG
                }
                channel.inst[44] = ym2203Register[0xb0 + c] & 0x07; // AL
                channel.inst[45] = (ym2203Register[0xb0 + c] & 0x38) >> 3; // FB
                channel.inst[46] = (ym2203Register[0xb4 + c] & 0x38) >> 4; // AMS
                channel.inst[47] = ym2203Register[0xb4 + c] & 0x07; // FMS

                channel.pan = 3;
                channel.slot = (byte)(fmKeyYM2203[ch] >> 4);

                int freq = 0;
                int octav;
                int n = -1;
                if (ch != 2 || !isFmEx)
                {
                    octav = (ym2203Register[0xa4 + c] & 0x38) >> 3;
                    freq = ym2203Register[0xa0 + c] + (ym2203Register[0xa4 + c] & 0x07) * 0x100;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (12 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    channel.slot = 0;
                    if ((fmKeyYM2203[ch] & 1) != 0)
                    {
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                        channel.slot = (byte)(fmKeyYM2203[ch] >> 4);
                    }

                    byte con = (byte)fmKeyYM2203[ch];
                    int v = 127;
                    int m = Md[ym2203Register[0xb0 + c] & 7];
                    v = ((con & 0x10) != 0 && (m & 0x10) != 0 && v > (ym2203Register[0x40 + c] & 0x7f)) ? (ym2203Register[0x40 + c] & 0x7f) : v; // OP1
                    v = ((con & 0x20) != 0 && (m & 0x20) != 0 && v > (ym2203Register[0x44 + c] & 0x7f)) ? (ym2203Register[0x44 + c] & 0x7f) : v; // OP3
                    v = ((con & 0x40) != 0 && (m & 0x40) != 0 && v > (ym2203Register[0x48 + c] & 0x7f)) ? (ym2203Register[0x48 + c] & 0x7f) : v; // OP2
                    v = ((con & 0x80) != 0 && (m & 0x80) != 0 && v > (ym2203Register[0x4c + c] & 0x7f)) ? (ym2203Register[0x4c + c] & 0x7f) : v; // OP4
                    channel.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ym2203Vol[ch] / 80.0), 0), 19);
                }
                else
                {
                    // ExAll hardcoded false (see file header) - m is always the chip-
                    // register-computed mask.
                    int m = Md[ym2203Register[0xb0 + 2] & 7];
                    freq = ym2203Register[0xa9] + (ym2203Register[0xad] & 0x07) * 0x100;
                    octav = (ym2203Register[0xad] & 0x38) >> 3;
                    newParam.channels[2].freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (12 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKeyYM2203[2] & 0x10) != 0 && (m & 0x10) != 0)
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    int v = (m & 0x10) != 0 ? ym2203Register[0x40 + c] : 127;
                    newParam.channels[2].volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ym2203Ch3SlotVol[0] / 80.0), 0), 19);
                }
                channel.note = n;
            }

            int[] exReg = { 2, 0, -6 };
            for (int ch = 3; ch < 6; ch++) // FM EX
            {
                int c = exReg[ch - 3];
                MDChipParams.Channel channel = newParam.channels[ch];
                channel.pan = 0;

                if (isFmEx)
                {
                    // ExAll hardcoded false (see file header).
                    int m = Md[ym2203Register[0xb0 + 2] & 7];
                    int op = ch - 2;
                    op = op == 1 ? 2 : (op == 2 ? 1 : op);

                    int freq = ym2203Register[0xa8 + c] + (ym2203Register[0xac + c] & 0x07) * 0x100;
                    int octav = (ym2203Register[0xac + c] & 0x38) >> 3;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    int n = -1;
                    if ((fmKeyYM2203[2] & (0x20 << (ch - 3))) != 0 && (m & (0x10 << op)) != 0)
                    {
                        float ff = freq / ((2 << 20) / (masterClock / (12 * fmDiv))) * (2 << (octav + 2));
                        ff /= 1038f;
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                    }
                    channel.note = n;

                    int v = (m & (0x10 << op)) != 0 ? ym2203Register[0x42 + op * 4] : 127;
                    channel.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ym2203Ch3SlotVol[ch - 2] / 80.0), 0), 19);
                }
                else
                {
                    channel.note = -1;
                    channel.volumeL = 0;
                }
            }

            for (int ch = 0; ch < 3; ch++) // SSG
            {
                MDChipParams.Channel channel = newParam.channels[ch + 6];

                bool t = (ym2203Register[0x07] & (0x1 << ch)) == 0;
                bool n = (ym2203Register[0x07] & (0x8 << ch)) == 0;
                channel.tn = (t ? 1 : 0) + (n ? 2 : 0);
                channel.volumeL = ym2203Register[0x08 + ch] & 0xf;
                channel.volume = (t || n) ? (ym2203Register[0x08 + ch] & 0xf) : 0;

                int ft = ym2203Register[0x00 + ch * 2];
                int ct = ym2203Register[0x01 + ch * 2];
                int tp = (ct << 8) | ft;
                channel.freq = tp;

                if (!t && !n && channel.volume > 0)
                {
                    channel.volume--;
                }

                if (channel.volume == 0 && (ym2203Register[0x08 + ch] & 0x10) == 0)
                {
                    channel.note = -1;
                }
                else
                {
                    if (tp == 0) tp = 1;
                    float ftone = (float)(7987200.0 / (64.0 * tp) * ssgMul);
                    channel.note = Common.searchSSGNote(ftone);
                }
                channel.ex = (ym2203Register[0x08 + ch] & 0xf0) != 0;
            }

            newParam.nfrq = ym2203Register[0x06] & 0x1f;
            newParam.efrq = ym2203Register[0x0c] * 0x100 + ym2203Register[0x0b];
            newParam.etype = ym2203Register[0x0d] & 0xf;
        }

        // frmYM2203.cs:289 screenDrawParams. Row-placement quirk kept exactly as the
        // original: the FM-EX channels' (3,4,5) Volume/KeyBoard/Slot/font4Hex16Bit calls
        // draw at row (c+3) (i.e. rows 6/7/8), but their ChYm2203 badge call passes the raw
        // loop variable `c` (3,4,5), which ChYm2203's own row math (8+ch*8) places at rows
        // 3/4/5 instead - a visual mismatch versus the rest of that channel's row, but
        // that's what frmYM2203.cs itself does (see DrawBuffYm2203.cs's ChYm2203 comment).
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 6; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                if (c == 2)
                {
                    DrawBuffYm2203.Volume(screen, 272, 8 + c * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2203.KeyBoard(screen, 32, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2203.Inst(screen, 1, 12, c, oyc.inst, nyc.inst);
                    DrawBuffYm2203.Ch3(screen, c, ref oyc.mask, nyc.mask, ref oyc.ex, nyc.ex);
                    DrawBuffYm2203.Slot(screen, 0 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2203.Font4Hex16Bit(screen, 0 + 4 * 78, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else if (c < 3)
                {
                    DrawBuffYm2203.Volume(screen, 272, 8 + c * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2203.KeyBoard(screen, 32, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2203.Inst(screen, 1, 12, c, oyc.inst, nyc.inst);
                    DrawBuffYm2203.ChYm2203(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2203.Slot(screen, 0 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2203.Font4Hex16Bit(screen, 0 + 4 * 78, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else
                {
                    DrawBuffYm2203.Volume(screen, 272, 8 + (c + 3) * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2203.KeyBoard(screen, 32, 8 + (c + 3) * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2203.ChYm2203(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2203.Slot(screen, 0 + 4 * 64, 8 + (c + 3) * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2203.Font4Hex16Bit(screen, 0 + 4 * 78, 8 + (c + 3) * 8, ref oyc.freq, nyc.freq);
                }
            }

            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 6];
                MDChipParams.Channel nyc = newParam.channels[c + 6];

                DrawBuffYm2203.VolumeShort(screen, 280 + 0, 8 + (c + 3) * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffYm2203.KeyBoard(screen, 32, 8 + (c + 3) * 8, ref oyc.note, nyc.note);
                DrawBuffYm2203.Tn(screen, 6, 2, c + 3, ref oyc.tn, nyc.tn, ref oyc.tntp, 0);

                DrawBuffYm2203.ChYm2203(screen, c + 6, ref oyc.mask, nyc.mask);
                DrawBuffYm2203.DrawNesSw(screen, 268 + 0, 8 + (c + 3) * 8, ref oyc.ex, nyc.ex);
                DrawBuffYm2203.Font4Hex16Bit(screen, 0 + 4 * 78, 8 + (c + 3) * 8, ref oyc.freq, nyc.freq);
                DrawBuffYm2203.Font4HexByte(screen, 272 + 0, 8 + (c + 3) * 8, ref oyc.volumeL, nyc.volumeL);
            }

            DrawBuffYm2203.Nfrq(screen, 5, 32, ref oldParam.nfrq, newParam.nfrq);
            DrawBuffYm2203.Efrq(screen, 18, 32, ref oldParam.efrq, newParam.efrq);
            DrawBuffYm2203.Etype(screen, 33, 32, ref oldParam.etype, newParam.etype);

            screen.Present();
        }
    }
}
