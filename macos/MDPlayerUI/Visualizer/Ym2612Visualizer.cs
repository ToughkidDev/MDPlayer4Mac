// Port of MDPlayer/MDPlayerx64/form/KB/OPN/frmYM2612.cs - the YM2612 (FM) channel
// visualizer: 6 FM channel rows (LED volume bars, piano keyboard, pan, per-operator
// instrument/operator-parameter table) + a plain-PCM display for channel 6 + the 3 extra
// "Ch3 special mode" operator slots exposed when the song puts channel 3 into extended
// mode + LFO/timer readouts. Scope for this round (see macos/README.md's chip-visualizer
// section): everything above, EXCLUDING the XGM/XGM2-specific PCM sample-player display
// for channel 6 (Ch6YM2612XGM/Ch6YM2612XGM2 in the original) - those only matter for the
// XGM/XGM2 MegaDrive driver formats, not the VGM/VGZ files this port's file picker
// actually feeds it, so channel 6 always takes frmYM2612.cs's plain "else" branch here.
//
// Data source: same approach as Sn76489Visualizer - reads chipRegister.fmRegisterYM2612/
// fmVolYM2612/fmCh3SlotVolYM2612/fmKeyOnYM2612 directly instead of through the original's
// unported Audio.GetFMRegister/GetFMVolume/GetFMCh3SlotVolume/GetFMKeyOn wrappers (those
// are thin pass-throughs to the same ChipRegister fields, which the port has near-1:1).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2612Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private readonly int ChipID;

        private readonly MDChipParams.YM2612 newParam = new();
        private readonly MDChipParams.YM2612 oldParam = new();

        public PixelScreen Screen => screen;

        // drawBuff.cs's md[] table (frmYM2612.cs:104) - per-algorithm bitmask of which
        // operator(s) are the audible "carrier" output(s), used by the channel volume-bar
        // estimate below (a carrier operator's Total Level roughly tracks how loud that
        // channel sounds - this is the same simplified heuristic the original UI uses, not
        // a true mixed-output level).
        private static readonly byte[] Md = new byte[]
        {
            0x08 << 4, 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x0c << 4, 0x0e << 4, 0x0e << 4, 0x0f << 4,
        };

        public Ym2612Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffYm2612.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYM2612");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2612.cs:116 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[][] fmRegister = chipRegister.fmRegisterYM2612[ChipID];
            int[] fmVol = chipRegister.fmVolYM2612[ChipID];
            int[] fmCh3SlotVol = chipRegister.fmCh3SlotVolYM2612[ChipID];
            int[] fmKey = chipRegister.fmKeyOnYM2612[ChipID];

            if (fmRegister == null || fmRegister[0] == null || fmKey == null) return;

            bool isFmEx = (fmRegister[0][0x27] & 0x40) != 0;
            newParam.channels[2].ex = isFmEx;

            newParam.lfoSw = (fmRegister[0][0x22] & 0x8) != 0;
            newParam.lfoFrq = fmRegister[0][0x22] & 0x7;
            newParam.timerA = fmRegister[0][0x24] | ((fmRegister[0][0x25] & 0x3) << 8);
            newParam.timerB = fmRegister[0][0x26];

            const int defaultMasterClock = 8000000;
            int masterClock = clockHz != 0 ? (int)clockHz : defaultMasterClock;
            const float fmDiv = 6;

            for (int ch = 0; ch < 6; ch++)
            {
                int p = ch > 2 ? 1 : 0;
                int c = ch > 2 ? ch - 3 : ch;
                MDChipParams.Channel nyc = newParam.channels[ch];
                nyc.slot = (byte)(fmKey[ch] >> 4);

                for (int i = 0; i < 4; i++)
                {
                    int ops = i == 0 ? 0 : (i == 1 ? 8 : (i == 2 ? 4 : 12));
                    nyc.inst[i * 11 + 0] = fmRegister[p][0x50 + ops + c] & 0x1f; // AR
                    nyc.inst[i * 11 + 1] = fmRegister[p][0x60 + ops + c] & 0x1f; // DR
                    nyc.inst[i * 11 + 2] = fmRegister[p][0x70 + ops + c] & 0x1f; // SR
                    nyc.inst[i * 11 + 3] = fmRegister[p][0x80 + ops + c] & 0x0f; // RR
                    nyc.inst[i * 11 + 4] = (fmRegister[p][0x80 + ops + c] & 0xf0) >> 4; // SL
                    nyc.inst[i * 11 + 5] = fmRegister[p][0x40 + ops + c] & 0x7f; // TL
                    nyc.inst[i * 11 + 6] = (fmRegister[p][0x50 + ops + c] & 0xc0) >> 6; // KS
                    nyc.inst[i * 11 + 7] = fmRegister[p][0x30 + ops + c] & 0x0f; // ML
                    nyc.inst[i * 11 + 8] = (fmRegister[p][0x30 + ops + c] & 0x70) >> 4; // DT
                    nyc.inst[i * 11 + 9] = (fmRegister[p][0x60 + ops + c] & 0x80) >> 7; // AM
                    nyc.inst[i * 11 + 10] = fmRegister[p][0x90 + ops + c] & 0x0f; // SG
                }
                nyc.inst[44] = fmRegister[p][0xb0 + c] & 0x07; // AL
                nyc.inst[45] = (fmRegister[p][0xb0 + c] & 0x38) >> 3; // FB
                nyc.inst[46] = (fmRegister[p][0xb4 + c] & 0x38) >> 4; // AMS
                nyc.inst[47] = fmRegister[p][0xb4 + c] & 0x07; // FMS

                nyc.pan = (fmRegister[p][0xb4 + c] & 0xc0) >> 6;

                int n = -1;
                if (ch != 2 || !isFmEx)
                {
                    int freq = fmRegister[p][0xa0 + c] + (fmRegister[p][0xa4 + c] & 0x07) * 0x100;
                    int octav = (fmRegister[p][0xa4 + c] & 0x38) >> 3;
                    nyc.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKey[ch] & 1) != 0)
                    {
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                    }

                    byte con = (byte)fmKey[ch];
                    int v = 127;
                    int m = Md[fmRegister[p][0xb0 + c] & 7];

                    v = (con & 0x10) != 0 && (m & 0x10) != 0 && v > (fmRegister[p][0x40 + c] & 0x7f) ? fmRegister[p][0x40 + c] & 0x7f : v;
                    v = (con & 0x20) != 0 && (m & 0x20) != 0 && v > (fmRegister[p][0x44 + c] & 0x7f) ? fmRegister[p][0x44 + c] & 0x7f : v;
                    v = (con & 0x40) != 0 && (m & 0x40) != 0 && v > (fmRegister[p][0x48 + c] & 0x7f) ? fmRegister[p][0x48 + c] & 0x7f : v;
                    v = (con & 0x80) != 0 && (m & 0x80) != 0 && v > (fmRegister[p][0x4c + c] & 0x7f) ? fmRegister[p][0x4c + c] & 0x7f : v;

                    nyc.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((fmRegister[p][0xb4 + c] & 0x80) != 0 ? 1 : 0) * fmVol[ch] / 80.0), 0), 19);
                    nyc.volumeR = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((fmRegister[p][0xb4 + c] & 0x40) != 0 ? 1 : 0) * fmVol[ch] / 80.0), 0), 19);
                }
                else
                {
                    int m = Md[fmRegister[0][0xb0 + 2] & 7];
                    int freq = fmRegister[0][0xa9] + (fmRegister[0][0xad] & 0x07) * 0x100;
                    int octav = (fmRegister[0][0xad] & 0x38) >> 3;
                    newParam.channels[2].freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKey[2] & 0x10) != 0 && (m & 0x10) != 0)
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    int v = (m & 0x10) != 0 ? fmRegister[p][0x40 + c] : 127;
                    newParam.channels[2].volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((fmRegister[0][0xb4 + 2] & 0x80) != 0 ? 1 : 0) * fmCh3SlotVol[0] / 80.0), 0), 19);
                    newParam.channels[2].volumeR = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((fmRegister[0][0xb4 + 2] & 0x40) != 0 ? 1 : 0) * fmCh3SlotVol[0] / 80.0), 0), 19);
                }
                nyc.note = n;
            }

            // Ch3 special-mode extended slots (operators driven independently while
            // channel 3 is in "special mode" - frmYM2612.cs's ch=6..8 loop). exReg maps
            // each extended slot to the byte offset (relative to 0xa8) of its own
            // frequency register; the 4th operator keeps using channel 3's normal
            // frequency register (handled by the ch==2 branch above), so there's no
            // exReg[3] entry.
            int[] exReg = { 2, 0, -6 };
            for (int ch = 6; ch < 9; ch++)
            {
                int c = exReg[ch - 6];
                MDChipParams.Channel nyc = newParam.channels[ch];
                nyc.pan = 0;

                if (isFmEx)
                {
                    int m = Md[fmRegister[0][0xb0 + 2] & 7];
                    int op = ch - 5;
                    op = op == 1 ? 2 : (op == 2 ? 1 : op);

                    int freq = fmRegister[0][0xa8 + c] + (fmRegister[0][0xac + c] & 0x07) * 0x100;
                    int octav = (fmRegister[0][0xac + c] & 0x38) >> 3;
                    nyc.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    int n = -1;
                    if ((fmKey[2] & (0x10 << (ch - 5))) != 0 && (m & (0x10 << op)) != 0)
                    {
                        float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                        ff /= 1038f;
                        n = System.Math.Min(System.Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                    }
                    nyc.note = n;

                    int v = (m & (0x10 << op)) != 0 ? fmRegister[0][0x42 + op * 4] : 127;
                    nyc.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * fmCh3SlotVol[ch - 5] / 80.0), 0), 19);
                }
                else
                {
                    nyc.note = -1;
                    nyc.volumeL = 0;
                }
            }

            newParam.channels[5].pcmMode = (fmRegister[0][0x2b] & 0x80) >> 7;
            if (newParam.channels[5].pcmBuff > 0) newParam.channels[5].pcmBuff--;
            if (newParam.channels[5].pcmMode != 0)
            {
                newParam.channels[5].volumeL = System.Math.Min(System.Math.Max(fmVol[5] / 80, 0), 19);
                newParam.channels[5].volumeR = System.Math.Min(System.Math.Max(fmVol[5] / 80, 0), 19);
            }
        }

        // frmYM2612.cs:337 screenDrawParams (plain/non-XGM path only - see file header).
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                if (c == 2)
                {
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2612.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2612.KeyBoardOPNM(screen, c, ref oyc.note, nyc.note);
                    DrawBuffYm2612.InstOPN2(screen, 13, 96, c, oyc.inst, nyc.inst);
                    DrawBuffYm2612.Ch3YM2612(screen, c, ref oyc.mask, nyc.mask, ref oyc.ex, nyc.ex);
                    DrawBuffYm2612.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2612.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else if (c < 5)
                {
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2612.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2612.KeyBoardOPNM(screen, c, ref oyc.note, nyc.note);
                    DrawBuffYm2612.InstOPN2(screen, 13, 96, c, oyc.inst, nyc.inst);
                    DrawBuffYm2612.ChYM2612(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2612.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2612.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else if (c == 5)
                {
                    DrawBuffYm2612.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                    DrawBuffYm2612.InstOPN2(screen, 13, 96, c, oyc.inst, nyc.inst);
                    DrawBuffYm2612.Ch6YM2612(screen, nyc.pcmBuff, ref oyc.pcmMode, nyc.pcmMode, ref oyc.mask, nyc.mask);
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2612.KeyBoardOPNM(screen, c, ref oyc.note, nyc.note);
                    DrawBuffYm2612.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2612.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else
                {
                    DrawBuffYm2612.Volume(screen, 289, 8 + c * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2612.KeyBoardOPNM(screen, c, ref oyc.note, nyc.note);
                    DrawBuffYm2612.ChYM2612(screen, c, ref oyc.mask, nyc.mask);
                    oyc.freq = 0;
                    DrawBuffYm2612.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
            }

            DrawBuffYm2612.LfoSw(screen, 16 + 1, 176, ref oldParam.lfoSw, newParam.lfoSw);
            DrawBuffYm2612.LfoFrq(screen, 64 + 1, 176, ref oldParam.lfoFrq, newParam.lfoFrq);
            DrawBuffYm2612.Font4Hex12Bit(screen, 1 + 29 * 4, 44 * 4, ref oldParam.timerA, newParam.timerA);
            DrawBuffYm2612.Font4HexByte(screen, 1 + 43 * 4, 44 * 4, ref oldParam.timerB, newParam.timerB);

            screen.Present();
        }
    }
}
