// Port of MDPlayer/MDPlayerx64/form/KB/OPL/frmY8950.cs - the Y8950 (MSX-AUDIO's FM+ADPCM
// chip - an OPL1/YM3526-compatible FM core plus a built-in ADPCM sample-playback channel)
// channel visualizer: 9 FM channel rows (own 2-operator table each, same shape as
// YM3526/YM3812 minus YM3812's Waveform Select field) + 5 fixed-role rhythm channels + 1
// ADPCM channel (LED volume bar + keyboard + badge only, no operator table - it plays back
// PCM samples, not FM). See DrawBuffY8950.cs's header for the row-layout quirk (ADPCM sits
// visually above the rhythm section, reusing keyboard row 9).
//
// Data source: reads chipRegister.fmRegisterY8950 directly and calls
// chipRegister.getY8950KeyInfo(chipID) once per frame - same pattern as every other OPL-
// family visualizer in this port.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Y8950Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.Y8950 newParam = new();
        private readonly MDChipParams.Y8950 oldParam = new();

        public PixelScreen Screen => screen;

        private static readonly int[] Slot1Tbl = { 0, 1, 2, 6, 7, 8, 12, 13, 14 };
        private static readonly int[] Slot2Tbl = { 3, 4, 5, 9, 10, 11, 15, 16, 17 };
        private static readonly byte[] RhythmAdr = { 0x53, 0x54, 0x52, 0x55, 0x51 };

        public Y8950Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffY8950.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeY8950");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmY8950.cs:150 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] y8950Register = chipRegister.fmRegisterY8950[ChipID];
            if (y8950Register == null) return;

            ChipKeyInfo ki = chipRegister.getY8950KeyInfo(ChipID);
            float masterClock = clockHz != 0 ? clockHz : 3579545f;

            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];

                for (int i = 0; i < 2; i++)
                {
                    int slot = i == 0 ? Slot1Tbl[c] : Slot2Tbl[c];
                    slot = (slot % 6) + 8 * (slot / 6);

                    nyc.inst[0 + i * 17] = y8950Register[0x60 + slot] >> 4; // AR
                    nyc.inst[1 + i * 17] = y8950Register[0x60 + slot] & 0xf; // DR
                    nyc.inst[2 + i * 17] = y8950Register[0x80 + slot] >> 4; // SL
                    nyc.inst[3 + i * 17] = y8950Register[0x80 + slot] & 0xf; // RR
                    nyc.inst[4 + i * 17] = y8950Register[0x40 + slot] >> 6; // KL
                    nyc.inst[5 + i * 17] = y8950Register[0x40 + slot] & 0x3f; // TL
                    nyc.inst[6 + i * 17] = y8950Register[0x20 + slot] & 0xf; // MT
                    nyc.inst[7 + i * 17] = y8950Register[0x20 + slot] >> 7; // AM
                    nyc.inst[8 + i * 17] = (y8950Register[0x20 + slot] >> 6) & 1; // VB
                    nyc.inst[9 + i * 17] = (y8950Register[0x20 + slot] >> 5) & 1; // EG
                    nyc.inst[10 + i * 17] = (y8950Register[0x20 + slot] >> 4) & 1; // KR
                }

                nyc.inst[11] = (y8950Register[0xb0 + c] >> 2) & 7; // BL
                nyc.inst[12] = y8950Register[0xa0 + c] + ((y8950Register[0xb0 + c] & 3) << 8); // FNUM
                nyc.inst[15] = (y8950Register[0xc0 + c] >> 1) & 7; // FB
                nyc.inst[14] = y8950Register[0xc0 + c] & 1; // CN

                // FNUM / (2^19) * (mClock/72) * (2 ^ (block - 1))
                double fmus = (double)nyc.inst[12] / (1 << 19) * (masterClock / 72.0) * (1 << nyc.inst[11]);
                nyc.note = Common.searchSegaPCMNote(fmus / 523.3); // 523.3 -> c4

                if (ki.On[c])
                {
                    int tl1 = nyc.inst[5 + 0 * 17];
                    int tl2 = nyc.inst[5 + 1 * 17];
                    int tl = tl2;
                    if (nyc.inst[14] != 0)
                    {
                        tl = System.Math.Min(tl1, tl2);
                    }
                    nyc.volume = 19 * (64 - tl) / 64;
                }
                else
                {
                    if ((y8950Register[0xb0 + c] & 0x20) == 0) nyc.note = -1;
                    nyc.volume--;
                    if (nyc.volume < 0) nyc.volume = 0;
                }
            }

            newParam.channels[9].dda = ((y8950Register[0xbd] >> 7) & 0x01) != 0; // DA
            newParam.channels[10].dda = ((y8950Register[0xbd] >> 6) & 0x01) != 0; // DV

            for (int i = 0; i < 5; i++)
            {
                if (ki.On[i + 9])
                {
                    newParam.channels[i + 9].volume = 19 - ((y8950Register[RhythmAdr[i]] & 0x3f) >> 2);
                }
                else
                {
                    newParam.channels[i + 9].volume--;
                    if (newParam.channels[i + 9].volume < 0) newParam.channels[i + 9].volume = 0;
                }
            }

            // ADPCM (channel 14) - Delta-N drives the playback sample rate, TL the volume.
            newParam.channels[14].inst[12] = y8950Register[0x10] + (y8950Register[0x11] << 8); // Delta

            if (ki.On[14])
            {
                // fSample = deltaN * 50KHz / (2^16)
                double fSample = newParam.channels[14].inst[12] * 50000.0 / (1 << 16);
                int pnt = Common.searchSegaPCMNote(fSample / 8000.0);

                if (newParam.channels[14].note != pnt)
                {
                    newParam.channels[14].note = pnt;
                    int tl = y8950Register[0x12];
                    newParam.channels[14].volume = pnt == -1 ? 0 : Common.Range(tl >> 3, 0, 19);
                }
                else
                {
                    newParam.channels[14].volume--;
                    if (newParam.channels[14].volume < 0) newParam.channels[14].volume = 0;
                }
            }

            newParam.channels[14].volume--;
            if (newParam.channels[14].volume <= 0)
            {
                newParam.channels[14].note = -1;
                newParam.channels[14].volume--;
                if (newParam.channels[14].volume < 0) newParam.channels[14].volume = 0;
            }
        }

        // frmY8950.cs:294 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                for (int i = 0; i < 2; i++)
                {
                    DrawBuffY8950.Font4Int2(screen, 16 + 4 + i * 136, c * 8 + 104, ref oyc.inst[0 + i * 17], nyc.inst[0 + i * 17]); // AR
                    DrawBuffY8950.Font4Int2(screen, 16 + 16 + i * 136, c * 8 + 104, ref oyc.inst[1 + i * 17], nyc.inst[1 + i * 17]); // DR
                    DrawBuffY8950.Font4Int2(screen, 16 + 28 + i * 136, c * 8 + 104, ref oyc.inst[2 + i * 17], nyc.inst[2 + i * 17]); // SL
                    DrawBuffY8950.Font4Int2(screen, 16 + 40 + i * 136, c * 8 + 104, ref oyc.inst[3 + i * 17], nyc.inst[3 + i * 17]); // RR
                    DrawBuffY8950.Font4Int2(screen, 16 + 52 + i * 136, c * 8 + 104, ref oyc.inst[4 + i * 17], nyc.inst[4 + i * 17]); // KL
                    DrawBuffY8950.Font4Int2(screen, 16 + 64 + i * 136, c * 8 + 104, ref oyc.inst[5 + i * 17], nyc.inst[5 + i * 17]); // TL
                    DrawBuffY8950.Font4Int2(screen, 16 + 76 + i * 136, c * 8 + 104, ref oyc.inst[6 + i * 17], nyc.inst[6 + i * 17]); // MT

                    DrawBuffY8950.Font4Int2(screen, 16 + 88 + i * 136, c * 8 + 104, ref oyc.inst[7 + i * 17], nyc.inst[7 + i * 17]); // AM
                    DrawBuffY8950.Font4Int2(screen, 16 + 96 + i * 136, c * 8 + 104, ref oyc.inst[8 + i * 17], nyc.inst[8 + i * 17]); // VB
                    DrawBuffY8950.Font4Int2(screen, 16 + 104 + i * 136, c * 8 + 104, ref oyc.inst[9 + i * 17], nyc.inst[9 + i * 17]); // EG
                    DrawBuffY8950.Font4Int2(screen, 16 + 112 + i * 136, c * 8 + 104, ref oyc.inst[10 + i * 17], nyc.inst[10 + i * 17]); // KR
                }

                DrawBuffY8950.Font4Int2(screen, 16 + 4 * 65, c * 8 + 104, ref oyc.inst[11], nyc.inst[11]); // BL
                DrawBuffY8950.Font4Hex12Bit(screen, 16 + 4 * 69, c * 8 + 104, ref oyc.inst[12], nyc.inst[12]); // F-Num
                DrawBuffY8950.Font4Int2(screen, 16 + 4 * 73, c * 8 + 104, ref oyc.inst[14], nyc.inst[14]); // CN
                DrawBuffY8950.Font4Int2(screen, 16 + 4 * 76, c * 8 + 104, ref oyc.inst[15], nyc.inst[15]); // FB
                DrawBuffY8950.KeyBoard(screen, c, ref oyc.note, nyc.note);
                DrawBuffY8950.VolumeXY(screen, 64, c * 2 + 2, 0, ref oyc.volume, nyc.volume);
                DrawBuffY8950.ChY8950(screen, c, ref oyc.mask, nyc.mask);
            }

            DrawBuffY8950.DrawNesSw(screen, 76 * 4, 11 * 8, ref oldParam.channels[9].dda, newParam.channels[9].dda); // DA
            DrawBuffY8950.DrawNesSw(screen, 80 * 4, 11 * 8, ref oldParam.channels[10].dda, newParam.channels[10].dda); // DV

            for (int c = 9; c < 14; c++)
            {
                DrawBuffY8950.ChY8950(screen, c, ref oldParam.channels[c].mask, newParam.channels[c].mask);
                DrawBuffY8950.VolumeXY(screen, 3 + (c - 9) * 15, 11 * 2, 0, ref oldParam.channels[c].volume, newParam.channels[c].volume);
            }

            // ADPCM (channel 14) - reuses keyboard/volume row-index 9, matching frmY8950.cs
            // exactly (see DrawBuffY8950.cs's header for why).
            DrawBuffY8950.KeyBoard(screen, 9, ref oldParam.channels[14].note, newParam.channels[14].note);
            DrawBuffY8950.VolumeXY(screen, 64, 9 * 2 + 2, 0, ref oldParam.channels[14].volume, newParam.channels[14].volume);
            DrawBuffY8950.ChY8950(screen, 14, ref oldParam.channels[14].mask, newParam.channels[14].mask);

            screen.Present();
        }
    }
}
