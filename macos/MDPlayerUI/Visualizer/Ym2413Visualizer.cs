// Port of MDPlayer/MDPlayerx64/form/KB/OPL/frmYM2413.cs - the YM2413 (OPLL, MSX-MUSIC/
// MSX-AUDIO's FM chip; the NES VRC7 expansion chip is register-compatible and reuses this
// same window in the original) channel visualizer: 9 melody FM channel rows (LED volume
// bar/keyboard/instrument-number/sustain-flags) + 5 fixed-role rhythm channel rows
// (BD/SD/TOM/CYM/HH, LED volume bar only) + the single shared hardware "user instrument"
// operator-parameter table (only meaningful when a melody channel is set to instrument #0 -
// register bank 0x00-0x07 is chip-wide, not per-channel, so only channel 0's `inst[4..27]`
// carries these values, matching the original exactly).
//
// Data source: reads chipRegister.fmRegisterYM2413 directly and calls
// chipRegister.getYM2413KeyInfo(chipID) once per frame (same as the original's
// Audio.GetYM2413Register/GetYM2413KeyInfo, thin pass-throughs to these same members) -
// same approach as every other chip visualizer in this port. getYM2413KeyInfo has a
// one-shot side effect (it clears each channel's `On` edge-trigger flag after reading, so a
// note-on is only reported for the single frame it happens), so - like the original - this
// port calls it exactly once per ScreenChangeParams, never more.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2413Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.YM2413 newParam = new();
        private readonly MDChipParams.YM2413 oldParam = new();

        public PixelScreen Screen => screen;

        public Ym2413Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;

            DrawBuffYm2413.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYM2413");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2413.cs:95 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] ym2413Register = chipRegister.fmRegisterYM2413[ChipID];
            if (ym2413Register == null) return;

            ChipKeyInfo ki = chipRegister.getYM2413KeyInfo(ChipID);

            for (int ch = 0; ch < 9; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];

                nyc.inst[0] = (ym2413Register[0x30 + ch] & 0xf0) >> 4;
                nyc.inst[1] = (ym2413Register[0x20 + ch] & 0x20) >> 5;
                nyc.inst[2] = (ym2413Register[0x20 + ch] & 0x10) >> 4;
                nyc.inst[3] = ym2413Register[0x30 + ch] & 0x0f;

                int freq = ym2413Register[0x10 + ch] + ((ym2413Register[0x20 + ch] & 0x1) << 8);
                int oct = (ym2413Register[0x20 + ch] & 0xe) >> 1;

                nyc.note = Common.searchSegaPCMNote(freq / 172.0) + (oct - 4) * 12;

                if (ki.On[ch])
                {
                    nyc.volumeL = 19 - nyc.inst[3];
                }
                else
                {
                    if (nyc.inst[2] == 0) nyc.note = -1;
                    nyc.volumeL--;
                    if (nyc.volumeL < 0) nyc.volumeL = 0;
                }
            }

            // BD
            if (ki.On[9])
            {
                newParam.channels[9].volume = 19 - (ym2413Register[0x36] & 0x0f);
            }
            else
            {
                newParam.channels[9].volume--;
                if (newParam.channels[9].volume < 0) newParam.channels[9].volume = 0;
            }

            // SD
            if (ki.On[10])
            {
                newParam.channels[10].volume = 19 - (ym2413Register[0x37] & 0x0f);
            }
            else
            {
                newParam.channels[10].volume--;
                if (newParam.channels[10].volume < 0) newParam.channels[10].volume = 0;
            }

            // TOM
            if (ki.On[11])
            {
                newParam.channels[11].volume = 19 - ((ym2413Register[0x38] & 0xf0) >> 4);
            }
            else
            {
                newParam.channels[11].volume--;
                if (newParam.channels[11].volume < 0) newParam.channels[11].volume = 0;
            }

            // CYM
            if (ki.On[12])
            {
                newParam.channels[12].volume = 19 - (ym2413Register[0x38] & 0x0f);
            }
            else
            {
                newParam.channels[12].volume--;
                if (newParam.channels[12].volume < 0) newParam.channels[12].volume = 0;
            }

            // HH
            if (ki.On[13])
            {
                newParam.channels[13].volume = 19 - ((ym2413Register[0x37] & 0xf0) >> 4);
            }
            else
            {
                newParam.channels[13].volume--;
                if (newParam.channels[13].volume < 0) newParam.channels[13].volume = 0;
            }

            newParam.channels[0].inst[4] = ym2413Register[0x02] & 0x3f; // TL
            newParam.channels[0].inst[5] = ym2413Register[0x03] & 0x07; // FB

            newParam.channels[0].inst[6] = (ym2413Register[0x04] & 0xf0) >> 4; // AR
            newParam.channels[0].inst[7] = ym2413Register[0x04] & 0x0f; // DR
            newParam.channels[0].inst[8] = (ym2413Register[0x06] & 0xf0) >> 4; // SL
            newParam.channels[0].inst[9] = ym2413Register[0x06] & 0x0f; // RR
            newParam.channels[0].inst[10] = (ym2413Register[0x02] & 0x80) >> 7; // KL
            newParam.channels[0].inst[11] = ym2413Register[0x00] & 0x0f; // MT
            newParam.channels[0].inst[12] = (ym2413Register[0x00] & 0x80) >> 7; // AM
            newParam.channels[0].inst[13] = (ym2413Register[0x00] & 0x40) >> 6; // VB
            newParam.channels[0].inst[14] = (ym2413Register[0x00] & 0x20) >> 5; // EG
            newParam.channels[0].inst[15] = (ym2413Register[0x00] & 0x10) >> 4; // KR
            newParam.channels[0].inst[16] = (ym2413Register[0x03] & 0x08) >> 3; // DM
            newParam.channels[0].inst[17] = (ym2413Register[0x05] & 0xf0) >> 4; // AR
            newParam.channels[0].inst[18] = ym2413Register[0x05] & 0x0f; // DR
            newParam.channels[0].inst[19] = (ym2413Register[0x07] & 0xf0) >> 4; // SL
            newParam.channels[0].inst[20] = ym2413Register[0x07] & 0x0f; // RR
            newParam.channels[0].inst[21] = (ym2413Register[0x03] & 0x80) >> 7; // KL
            newParam.channels[0].inst[22] = ym2413Register[0x01] & 0x0f; // MT
            newParam.channels[0].inst[23] = (ym2413Register[0x01] & 0x80) >> 7; // AM
            newParam.channels[0].inst[24] = (ym2413Register[0x01] & 0x40) >> 6; // VB
            newParam.channels[0].inst[25] = (ym2413Register[0x01] & 0x20) >> 5; // EG
            newParam.channels[0].inst[26] = (ym2413Register[0x01] & 0x10) >> 4; // KR
            newParam.channels[0].inst[27] = (ym2413Register[0x03] & 0x10) >> 4; // DC
        }

        // frmYM2413.cs:253 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffYm2413.Volume(screen, 256, 8 + c * 8, 0, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2413.KeyBoard(screen, c, ref oyc.note, nyc.note);

                DrawBuffYm2413.DrawInstNumber(screen, (c % 3) * 16 + 37, (c / 3) * 2 + 24, ref oyc.inst[0], nyc.inst[0]);
                DrawBuffYm2413.SusFlag(screen, (c % 3) * 16 + 41, (c / 3) * 2 + 24, 0, ref oyc.inst[1], nyc.inst[1]);
                DrawBuffYm2413.SusFlag(screen, (c % 3) * 16 + 44, (c / 3) * 2 + 24, 0, ref oyc.inst[2], nyc.inst[2]);
                DrawBuffYm2413.DrawInstNumber(screen, (c % 3) * 16 + 46, (c / 3) * 2 + 24, ref oyc.inst[3], nyc.inst[3]);

                DrawBuffYm2413.ChYm2413(screen, c, ref oyc.mask, nyc.mask);
            }

            for (int c = 9; c < 14; c++)
            {
                DrawBuffYm2413.ChYm2413(screen, c, ref oldParam.channels[c].mask, newParam.channels[c].mask);
            }

            DrawBuffYm2413.VolumeXY(screen, 6, 20, 0, ref oldParam.channels[9].volume, newParam.channels[9].volume);
            DrawBuffYm2413.VolumeXY(screen, 21, 20, 0, ref oldParam.channels[10].volume, newParam.channels[10].volume);
            DrawBuffYm2413.VolumeXY(screen, 36, 20, 0, ref oldParam.channels[11].volume, newParam.channels[11].volume);
            DrawBuffYm2413.VolumeXY(screen, 51, 20, 0, ref oldParam.channels[12].volume, newParam.channels[12].volume);
            DrawBuffYm2413.VolumeXY(screen, 66, 20, 0, ref oldParam.channels[13].volume, newParam.channels[13].volume);

            MDChipParams.Channel oyc0 = oldParam.channels[0];
            MDChipParams.Channel nyc0 = newParam.channels[0];
            DrawBuffYm2413.DrawInstNumber(screen, 9, 22, ref oyc0.inst[4], nyc0.inst[4]); // TL
            DrawBuffYm2413.DrawInstNumber(screen, 14, 22, ref oyc0.inst[5], nyc0.inst[5]); // FB

            for (int c = 0; c < 11; c++)
            {
                DrawBuffYm2413.DrawInstNumber(screen, c * 3, 26, ref oyc0.inst[6 + c], nyc0.inst[6 + c]);
                DrawBuffYm2413.DrawInstNumber(screen, c * 3, 28, ref oyc0.inst[17 + c], nyc0.inst[17 + c]);
            }

            screen.Present();
        }
    }
}
