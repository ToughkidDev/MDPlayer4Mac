// Port of MDPlayer/MDPlayerx64/form/KB/OPL/frmYM3526.cs - the YM3526 (OPL/OPL1) channel
// visualizer: 9 FM channel rows, each with its own full 2-operator parameter table (AR/DR/
// SL/RR/KL/TL/MT/AM/VB/EG/KR per operator, plus per-channel BL/F-Num/CN/FB) + LED volume
// bar + keyboard, plus 5 fixed-role rhythm channels (BD/SD/TOM/CYM/HH, volume bar only) and
// 2 ADPCM-related on/off flags (DA/DV).
//
// Data source: reads chipRegister.fmRegisterYM3526 directly and calls
// chipRegister.getYM3526KeyInfo(chipID) once per frame - same pattern as YM2413's
// getYM2413KeyInfo (see that visualizer's header for the one-shot-edge-trigger caveat).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym3526Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.YM3526 newParam = new();
        private readonly MDChipParams.YM3526 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYM3526.cs:101-103 - operator-slot lookup tables and the rhythm-channel
        // register-address table (BD/SD/TOM/CYM/HH order, matching the loop index 0..4).
        private static readonly int[] Slot1Tbl = { 0, 1, 2, 6, 7, 8, 12, 13, 14 };
        private static readonly int[] Slot2Tbl = { 3, 4, 5, 9, 10, 11, 15, 16, 17 };
        private static readonly byte[] RhythmAdr = { 0x53, 0x54, 0x52, 0x55, 0x51 };

        public Ym3526Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffYm3526.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYM3526");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM3526.cs:105 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] ym3526Register = chipRegister.fmRegisterYM3526[ChipID];
            if (ym3526Register == null) return;

            ChipKeyInfo ki = chipRegister.getYM3526KeyInfo(ChipID);
            float masterClock = clockHz != 0 ? clockHz : 3579545f;

            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];

                for (int i = 0; i < 2; i++)
                {
                    int slot = i == 0 ? Slot1Tbl[c] : Slot2Tbl[c];
                    slot = (slot % 6) + 8 * (slot / 6);

                    nyc.inst[0 + i * 17] = ym3526Register[0x60 + slot] >> 4; // AR
                    nyc.inst[1 + i * 17] = ym3526Register[0x60 + slot] & 0xf; // DR
                    nyc.inst[2 + i * 17] = ym3526Register[0x80 + slot] >> 4; // SL
                    nyc.inst[3 + i * 17] = ym3526Register[0x80 + slot] & 0xf; // RR
                    nyc.inst[4 + i * 17] = ym3526Register[0x40 + slot] >> 6; // KL
                    nyc.inst[5 + i * 17] = ym3526Register[0x40 + slot] & 0x3f; // TL
                    nyc.inst[6 + i * 17] = ym3526Register[0x20 + slot] & 0xf; // MT
                    nyc.inst[7 + i * 17] = ym3526Register[0x20 + slot] >> 7; // AM
                    nyc.inst[8 + i * 17] = (ym3526Register[0x20 + slot] >> 6) & 1; // VB
                    nyc.inst[9 + i * 17] = (ym3526Register[0x20 + slot] >> 5) & 1; // EG
                    nyc.inst[10 + i * 17] = (ym3526Register[0x20 + slot] >> 4) & 1; // KR
                }

                nyc.inst[11] = (ym3526Register[0xb0 + c] >> 2) & 7; // BL
                nyc.inst[12] = ym3526Register[0xa0 + c] + ((ym3526Register[0xb0 + c] & 3) << 8); // FNUM
                nyc.inst[15] = (ym3526Register[0xc0 + c] >> 1) & 7; // FB
                nyc.inst[14] = ym3526Register[0xc0 + c] & 1; // CN

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
                    if ((ym3526Register[0xb0 + c] & 0x20) == 0) nyc.note = -1;
                    nyc.volume--;
                    if (nyc.volume < 0) nyc.volume = 0;
                }
            }

            newParam.channels[9].dda = ((ym3526Register[0xbd] >> 7) & 0x01) != 0; // DA
            newParam.channels[10].dda = ((ym3526Register[0xbd] >> 6) & 0x01) != 0; // DV

            // slot14 TL 0x51 HH / slot15 TL 0x52 TOM / slot16 TL 0x53 BD / slot17 TL 0x54 SD
            // / slot18 TL 0x55 CYM
            for (int i = 0; i < 5; i++)
            {
                if (ki.On[i + 9])
                {
                    newParam.channels[i + 9].volume = 19 - ((ym3526Register[RhythmAdr[i]] & 0x3f) >> 2);
                }
                else
                {
                    newParam.channels[i + 9].volume--;
                    if (newParam.channels[i + 9].volume < 0) newParam.channels[i + 9].volume = 0;
                }
            }
        }

        // frmYM3526.cs:217 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 9; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                for (int i = 0; i < 2; i++)
                {
                    DrawBuffYm3526.Font4Int2(screen, 16 + 4 + i * 132, c * 8 + 96, ref oyc.inst[0 + i * 17], nyc.inst[0 + i * 17]); // AR
                    DrawBuffYm3526.Font4Int2(screen, 16 + 12 + i * 132, c * 8 + 96, ref oyc.inst[1 + i * 17], nyc.inst[1 + i * 17]); // DR
                    DrawBuffYm3526.Font4Int2(screen, 16 + 20 + i * 132, c * 8 + 96, ref oyc.inst[2 + i * 17], nyc.inst[2 + i * 17]); // SL
                    DrawBuffYm3526.Font4Int2(screen, 16 + 28 + i * 132, c * 8 + 96, ref oyc.inst[3 + i * 17], nyc.inst[3 + i * 17]); // RR

                    DrawBuffYm3526.Font4Int2(screen, 16 + 40 + i * 132, c * 8 + 96, ref oyc.inst[4 + i * 17], nyc.inst[4 + i * 17]); // KL
                    DrawBuffYm3526.Font4Int2(screen, 16 + 48 + i * 132, c * 8 + 96, ref oyc.inst[5 + i * 17], nyc.inst[5 + i * 17]); // TL

                    DrawBuffYm3526.Font4Int2(screen, 16 + 60 + i * 132, c * 8 + 96, ref oyc.inst[6 + i * 17], nyc.inst[6 + i * 17]); // MT

                    DrawBuffYm3526.Font4Int2(screen, 16 + 72 + i * 132, c * 8 + 96, ref oyc.inst[7 + i * 17], nyc.inst[7 + i * 17]); // AM
                    DrawBuffYm3526.Font4Int2(screen, 16 + 80 + i * 132, c * 8 + 96, ref oyc.inst[8 + i * 17], nyc.inst[8 + i * 17]); // VB
                    DrawBuffYm3526.Font4Int2(screen, 16 + 88 + i * 132, c * 8 + 96, ref oyc.inst[9 + i * 17], nyc.inst[9 + i * 17]); // EG
                    DrawBuffYm3526.Font4Int2(screen, 16 + 96 + i * 132, c * 8 + 96, ref oyc.inst[10 + i * 17], nyc.inst[10 + i * 17]); // KR
                }

                DrawBuffYm3526.Font4Int2(screen, 16 + 4 * 64, c * 8 + 96, ref oyc.inst[11], nyc.inst[11]); // BL
                DrawBuffYm3526.Font4Hex12Bit(screen, 16 + 4 * 68, c * 8 + 96, ref oyc.inst[12], nyc.inst[12]); // F-Num
                DrawBuffYm3526.Font4Int2(screen, 16 + 4 * 72, c * 8 + 96, ref oyc.inst[14], nyc.inst[14]); // CN
                DrawBuffYm3526.Font4Int2(screen, 16 + 4 * 75, c * 8 + 96, ref oyc.inst[15], nyc.inst[15]); // FB
                DrawBuffYm3526.KeyBoard(screen, c, ref oyc.note, nyc.note);
                DrawBuffYm3526.VolumeXY(screen, 64, c * 2 + 2, 0, ref oyc.volume, nyc.volume);
                DrawBuffYm3526.ChYm3526(screen, c, ref oyc.mask, nyc.mask);
            }

            DrawBuffYm3526.DrawNesSw(screen, 76 * 4, 10 * 8, ref oldParam.channels[9].dda, newParam.channels[9].dda); // DA
            DrawBuffYm3526.DrawNesSw(screen, 80 * 4, 10 * 8, ref oldParam.channels[10].dda, newParam.channels[10].dda); // DV

            for (int c = 9; c < 14; c++)
            {
                DrawBuffYm3526.ChYm3526(screen, c, ref oldParam.channels[c].mask, newParam.channels[c].mask);
                DrawBuffYm3526.VolumeXY(screen, 3 + (c - 9) * 15, 10 * 2, 0, ref oldParam.channels[c].volume, newParam.channels[c].volume);
            }

            screen.Present();
        }
    }
}
