// Port of MDPlayer/MDPlayerx64/form/KB/OPL/frmYMF262.cs - the YMF262 (OPL3, the AdLib
// Gold/Sound Blaster 16's FM chip) channel visualizer: 18 FM channels (2 register ports x
// 9 channels, addresses 0x00-0xff each) with optional pairing into 4-operator mode
// (ConnectSelect, port-1 register 0x04) and genuine per-channel stereo pan, plus the same
// 5 fixed-role rhythm channels (BD/SD/TOM/CYM/HH) as the other OPL-family chips.
//
// Data source: reads chipRegister.fmRegisterYMF262[chipID] (int[2][0x100], one array per
// register port) directly, and calls chipRegister.getYMF262FMKeyON(chipID) (a live bitmask,
// NOT one-shot/edge-triggered - unlike every other OPL-family KeyInfo getter in this port)
// and chipRegister.getYMF262RyhthmKeyON(chipID) (IS one-shot; clears its state after read,
// same as the other chips' rhythm/key getters) once per frame.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ymf262Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.YMF262 newParam = new();
        private readonly MDChipParams.YMF262 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYMF262.cs:100-102.
        private static readonly int[] Slot1Tbl = { 0, 6, 1, 7, 2, 8, 12, 13, 14, 18, 24, 19, 25, 20, 26, 30, 31, 32 };
        private static readonly int[] Slot2Tbl = { 3, 9, 4, 10, 5, 11, 15, 16, 17, 21, 27, 22, 28, 23, 29, 33, 34, 35 };
        private static readonly int[] ChTbl = { 0, 3, 1, 4, 2, 5, 6, 7, 8 };

        public Ymf262Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;

            DrawBuffYmf262.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYMF262");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYMF262.cs:120 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[][] ymf262Register = chipRegister.fmRegisterYMF262[ChipID];
            if (ymf262Register == null) return;

            //FM
            for (int c = 0; c < 18; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];
                for (int i = 0; i < 2; i++)
                {
                    int slot;
                    int slotP;
                    if (i == 0)
                    {
                        slot = Slot1Tbl[c] % 18;
                        slotP = Slot1Tbl[c] / 18;
                    }
                    else
                    {
                        slot = Slot2Tbl[c] % 18;
                        slotP = Slot2Tbl[c] / 18;
                    }
                    slot = (slot % 6) + 8 * (slot / 6);

                    nyc.inst[0 + i * 17] = ymf262Register[slotP][0x60 + slot] >> 4; // AR
                    nyc.inst[1 + i * 17] = ymf262Register[slotP][0x60 + slot] & 0xf; // DR
                    nyc.inst[2 + i * 17] = ymf262Register[slotP][0x80 + slot] >> 4; // SL
                    nyc.inst[3 + i * 17] = ymf262Register[slotP][0x80 + slot] & 0xf; // RR
                    nyc.inst[4 + i * 17] = ymf262Register[slotP][0x40 + slot] >> 6; // KL
                    nyc.inst[5 + i * 17] = ymf262Register[slotP][0x40 + slot] & 0x3f; // TL
                    nyc.inst[6 + i * 17] = ymf262Register[slotP][0x20 + slot] & 0xf; // MT
                    nyc.inst[7 + i * 17] = ymf262Register[slotP][0x20 + slot] >> 7; // AM
                    nyc.inst[8 + i * 17] = (ymf262Register[slotP][0x20 + slot] >> 6) & 1; // VB
                    nyc.inst[9 + i * 17] = (ymf262Register[slotP][0x20 + slot] >> 5) & 1; // EG
                    nyc.inst[10 + i * 17] = (ymf262Register[slotP][0x20 + slot] >> 4) & 1; // KR
                    nyc.inst[13 + i * 17] = ymf262Register[slotP][0xe0 + slot] & 7; // WS
                }
            }

            newParam.channels[18].dda = ((ymf262Register[1][0xbd] >> 7) & 0x01) != 0; // DA
            newParam.channels[19].dda = ((ymf262Register[1][0xbd] >> 6) & 0x01) != 0; // DV

            // ConnectSelect - pairs adjacent channels into 4-operator mode.
            for (int c = 0; c < 6; c++)
            {
                newParam.channels[c].dda = (ymf262Register[1][0x04] & (0x1 << c)) != 0;
                newParam.channels[c].inst[34] = newParam.channels[c].dda ? 1 : 0; // [
                newParam.channels[c].inst[35] = newParam.channels[c].dda ? 2 : 0; // ]
                if (newParam.channels[c].dda)
                {
                    // OP4 mode.
                    int ch = (c < 3) ? c * 2 : ((c - 3) * 2 + 9);
                    int a = newParam.channels[ch].inst[14] * 2 + newParam.channels[ch].inst[14 + 17]; // cnt=14
                    switch (a) // mod=16
                    {
                        case 0:
                            newParam.channels[ch].inst[16] = 0;
                            newParam.channels[ch].inst[16 + 17] = 0;
                            newParam.channels[ch + 1].inst[16] = 0;
                            newParam.channels[ch + 1].inst[16 + 17] = 1;
                            break;
                        case 1:
                            newParam.channels[ch].inst[16] = 0;
                            newParam.channels[ch].inst[16 + 17] = 1;
                            newParam.channels[ch + 1].inst[16] = 0;
                            newParam.channels[ch + 1].inst[16 + 17] = 1;
                            break;
                        case 2:
                            newParam.channels[ch].inst[16] = 1;
                            newParam.channels[ch].inst[16 + 17] = 0;
                            newParam.channels[ch + 1].inst[16] = 0;
                            newParam.channels[ch + 1].inst[16 + 17] = 1;
                            break;
                        case 3:
                            newParam.channels[ch].inst[16] = 1;
                            newParam.channels[ch].inst[16 + 17] = 0;
                            newParam.channels[ch + 1].inst[16] = 1;
                            newParam.channels[ch + 1].inst[16 + 17] = 1;
                            break;
                    }
                }
            }

            int ko = chipRegister.getYMF262FMKeyON(ChipID);

            for (int c = 0; c < 18; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];

                int p = c / 9;
                bool isOp4 = false;
                int adr = c % 9;
                if (adr < 6)
                {
                    if (newParam.channels[(adr / 2) + p * 3].dda) isOp4 = true;
                }
                _ = isOp4; // kadr mirrors the original's unused-kadr transcription (see below)
                adr = ChTbl[adr];

                nyc.inst[11] = (ymf262Register[p][0xb0 + adr] >> 2) & 7; // BL
                nyc.inst[12] = ymf262Register[p][0xa0 + adr] + ((ymf262Register[p][0xb0 + adr] & 3) << 8); // FNUM

                nyc.inst[15] = (ymf262Register[p][0xc0 + adr] >> 1) & 7; // FB
                nyc.inst[14] = ymf262Register[p][0xc0 + adr] & 1; // CN

                nyc.inst[36] = ymf262Register[p][0xc0 + adr] & 0x30; // PAN
                nyc.inst[36] = ((nyc.inst[36] >> 5) & 1) | ((nyc.inst[36] >> 3) & 2); // 00RL0000 -> 000000LR

                int n = ymf262Register[p][0xc0 + adr] & 1; // modFlg
                nyc.inst[16] = n == 0 ? 0 : 1;
                nyc.inst[33] = 1;

                int nt = Common.searchSegaPCMNote(nyc.inst[12] / 344.0) + (nyc.inst[11] - 4) * 12;
                if ((ko & (1 << (adr + p * 9))) != 0)
                {
                    if (nyc.note != nt)
                    {
                        nyc.note = nt;
                        int tl1 = nyc.inst[5 + 0 * 17];
                        int tl2 = nyc.inst[5 + 1 * 17];
                        int tl = tl2;
                        if (n != 0)
                        {
                            tl = System.Math.Min(tl1, tl2);
                        }
                        nyc.volumeL = (nyc.inst[36] & 2) != 0 ? (19 * (64 - tl) / 64) : 0;
                        nyc.volumeR = (nyc.inst[36] & 1) != 0 ? (19 * (64 - tl) / 64) : 0;
                    }
                    else
                    {
                        nyc.volumeL--; if (nyc.volumeL < 0) nyc.volumeL = 0;
                        nyc.volumeR--; if (nyc.volumeR < 0) nyc.volumeR = 0;
                    }
                }
                else
                {
                    nyc.note = -1;
                    nyc.volumeL--; if (nyc.volumeL < 0) nyc.volumeL = 0;
                    nyc.volumeR--; if (nyc.volumeR < 0) nyc.volumeR = 0;
                }
            }

            // Rhythm section.
            int r = chipRegister.getYMF262RyhthmKeyON(ChipID);

            // slot16 TL 0x53 BD, slot17 TL 0x54 SD, slot15 TL 0x52 TOM, slot18 TL 0x55 CYM,
            // slot14 TL 0x51 HH.
            if ((r & 0x10) != 0)
            {
                newParam.channels[18].volume = 19 - ((ymf262Register[0][0x53] & 0x3f) >> 2); // BD
            }
            else
            {
                newParam.channels[18].volume--;
                if (newParam.channels[18].volume < 0) newParam.channels[18].volume = 0;
            }

            if ((r & 0x08) != 0)
            {
                newParam.channels[19].volume = 19 - ((ymf262Register[0][0x54] & 0x3f) >> 2); // SD
            }
            else
            {
                newParam.channels[19].volume--;
                if (newParam.channels[19].volume < 0) newParam.channels[19].volume = 0;
            }

            if ((r & 0x04) != 0)
            {
                newParam.channels[20].volume = 19 - ((ymf262Register[0][0x52] & 0x3f) >> 2); // TOM
            }
            else
            {
                newParam.channels[20].volume--;
                if (newParam.channels[20].volume < 0) newParam.channels[20].volume = 0;
            }

            if ((r & 0x02) != 0)
            {
                newParam.channels[21].volume = 19 - ((ymf262Register[0][0x55] & 0x3f) >> 2); // CYM
            }
            else
            {
                newParam.channels[21].volume--;
                if (newParam.channels[21].volume < 0) newParam.channels[21].volume = 0;
            }

            if ((r & 0x01) != 0)
            {
                newParam.channels[22].volume = 19 - ((ymf262Register[0][0x51] & 0x3f) >> 2); // HH
            }
            else
            {
                newParam.channels[22].volume--;
                if (newParam.channels[22].volume < 0) newParam.channels[22].volume = 0;
            }
        }

        // frmYMF262.cs:356 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 18; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                for (int i = 0; i < 2; i++)
                {
                    DrawBuffYmf262.SusFlag(screen, 2 + i * 33, c * 2 + 42, 1, ref oyc.inst[16 + i * 17], nyc.inst[16 + i * 17]);
                    DrawBuffYmf262.Font4Int2(screen, 16 + 4 + i * 132, c * 8 + 168, ref oyc.inst[0 + i * 17], nyc.inst[0 + i * 17]); // AR
                    DrawBuffYmf262.Font4Int2(screen, 16 + 12 + i * 132, c * 8 + 168, ref oyc.inst[1 + i * 17], nyc.inst[1 + i * 17]); // DR
                    DrawBuffYmf262.Font4Int2(screen, 16 + 20 + i * 132, c * 8 + 168, ref oyc.inst[2 + i * 17], nyc.inst[2 + i * 17]); // SL
                    DrawBuffYmf262.Font4Int2(screen, 16 + 28 + i * 132, c * 8 + 168, ref oyc.inst[3 + i * 17], nyc.inst[3 + i * 17]); // RR

                    DrawBuffYmf262.Font4Int2(screen, 16 + 40 + i * 132, c * 8 + 168, ref oyc.inst[4 + i * 17], nyc.inst[4 + i * 17]); // KL
                    DrawBuffYmf262.Font4Int2(screen, 16 + 48 + i * 132, c * 8 + 168, ref oyc.inst[5 + i * 17], nyc.inst[5 + i * 17]); // TL

                    DrawBuffYmf262.Font4Int2(screen, 16 + 60 + i * 132, c * 8 + 168, ref oyc.inst[6 + i * 17], nyc.inst[6 + i * 17]); // MT

                    DrawBuffYmf262.Font4Int2(screen, 16 + 72 + i * 132, c * 8 + 168, ref oyc.inst[7 + i * 17], nyc.inst[7 + i * 17]); // AM
                    DrawBuffYmf262.Font4Int2(screen, 16 + 80 + i * 132, c * 8 + 168, ref oyc.inst[8 + i * 17], nyc.inst[8 + i * 17]); // VB
                    DrawBuffYmf262.Font4Int2(screen, 16 + 88 + i * 132, c * 8 + 168, ref oyc.inst[9 + i * 17], nyc.inst[9 + i * 17]); // EG
                    DrawBuffYmf262.Font4Int2(screen, 16 + 96 + i * 132, c * 8 + 168, ref oyc.inst[10 + i * 17], nyc.inst[10 + i * 17]); // KR
                    DrawBuffYmf262.Font4Int2(screen, 16 + 108 + i * 132, c * 8 + 168, ref oyc.inst[13 + i * 17], nyc.inst[13 + i * 17]); // WS
                }

                DrawBuffYmf262.Font4Int2(screen, 16 + 4 * 64, c * 8 + 168, ref oyc.inst[11], nyc.inst[11]); // BL
                DrawBuffYmf262.Font4Hex12Bit(screen, 16 + 4 * 68, c * 8 + 168, ref oyc.inst[12], nyc.inst[12]); // F-Num
                DrawBuffYmf262.Font4Int2(screen, 16 + 4 * 72, c * 8 + 168, ref oyc.inst[14], nyc.inst[14]); // CN
                DrawBuffYmf262.Font4Int2(screen, 16 + 4 * 75, c * 8 + 168, ref oyc.inst[15], nyc.inst[15]); // FB
                int dmy = 99;
                DrawBuffYmf262.Pan(screen, 24, 8 + c * 8, ref oyc.inst[36], nyc.inst[36], ref dmy, 0);
                DrawBuffYmf262.KeyBoard(screen, c, ref oyc.note, nyc.note);
                DrawBuffYmf262.VolumeXY(screen, 64, c * 2 + 2, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYmf262.VolumeXY(screen, 64, c * 2 + 3, 1, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYmf262.ChYmf262(screen, c, ref oyc.mask, nyc.mask);
            }

            for (int c = 0; c < 6; c++)
            {
                DrawBuffYmf262.DrawNesSw(screen, 4 * 4 + c * 4, 39 * 8, ref oldParam.channels[c].dda, newParam.channels[c].dda); // CS
                int ch = (c < 3) ? c * 2 : ((c - 3) * 2 + 9);
                _ = ch; // ch mirrors the original's local var (unused after computation there too)
                DrawBuffYmf262.Kakko(screen, 4 * 0, (c < 3 ? 0 : 24) + c * 16 + 168, 0, ref oldParam.channels[c].inst[34], newParam.channels[c].inst[34]);
                DrawBuffYmf262.Kakko(screen, 4 * 163, (c < 3 ? 0 : 24) + c * 16 + 168, 0, ref oldParam.channels[c].inst[35], newParam.channels[c].inst[35]);
            }
            DrawBuffYmf262.DrawNesSw(screen, 13 * 4, 39 * 8, ref oldParam.channels[18].dda, newParam.channels[18].dda); // DA
            DrawBuffYmf262.DrawNesSw(screen, 17 * 4, 39 * 8, ref oldParam.channels[19].dda, newParam.channels[19].dda); // DV

            for (int c = 18; c < 23; c++)
            {
                DrawBuffYmf262.ChYmf262(screen, c, ref oldParam.channels[c].mask, newParam.channels[c].mask);
                DrawBuffYmf262.VolumeXY(screen, 6 + (c - 18) * 15, 19 * 2, 0, ref oldParam.channels[c].volume, newParam.channels[c].volume);
            }

            screen.Present();
        }
    }
}
