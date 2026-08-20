// Port of MDPlayer/MDPlayerx64/form/KB/OPL/frmYMF278B.cs - the YMF278B (OPL4) channel
// visualizer: the same 18 FM (2 register ports, ConnectSelect 4-op pairing, stereo pan) + 5
// rhythm channel structure as YMF262 (register ports 0/1), plus an entirely new 24-channel
// PCM/wavetable section (register port 2) with its own ADSR envelope, vibrato/LFO, reverb,
// pan, and a wider (15-octave) keyboard range.
//
// Data source: reads chipRegister.fmRegisterYMF278B[chipID] (int[3][0x100], one array per
// register port - ports 0/1 are the FM side, port 2 is the PCM side) directly, and calls:
//  - chipRegister.getYMF278BFMKeyON(chipID): live bitmask, NOT one-shot (same as YMF262's
//    FM KeyON - the original never calls a reset for it either, see frmYMF278B.cs:313's
//    commented-out Audio.resetYMF278BFMKeyON call).
//  - chipRegister.getYMF278BRyhthmKeyON(chipID) + explicit
//    chipRegister.resetYMF278BRyhthmKeyON(chipID) afterwards: unlike YMF262's rhythm
//    KeyON (which self-clears inside the getter), YMF278B's needs an explicit reset call
//    every frame - matching frmYMF278B.cs:378's Audio.ResetYMF278BRyhthmKeyON(chipID).
//  - chipRegister.getYMF278BPCMKeyON(chipID) + explicit
//    chipRegister.resetYMF278BPCMKeyON(chipID) afterwards, same one-shot-via-explicit-reset
//    pattern, matching frmYMF278B.cs:468.
//
// Deliberate simplification: the original also branches on Audio.GetMoonDriverPCMKeyOn()
// (MoonDriver's own softsynth driver exposes PCM key-on state directly) and uses it instead
// of getYMF278BPCMKeyON when a MoonDriver-family format is loaded. This port has never
// implemented MoonDriver (MGS/MuSICA/NDP/FMP/PMD/etc. - see MusicEngine.cs's header for the
// supported-format list), so that branch is unreachable here; this file always takes the
// "moonDriver以外" (non-MoonDriver) path, reading getYMF278BPCMKeyON directly - correct for
// every format this port actually loads.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ymf278bVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.YMF278B newParam = new();
        private readonly MDChipParams.YMF278B oldParam = new();

        public PixelScreen Screen => screen;

        // frmYMF278B.cs:92-94.
        private static readonly int[] Slot1Tbl = { 0, 6, 1, 7, 2, 8, 12, 13, 14, 18, 24, 19, 25, 20, 26, 30, 31, 32 };
        private static readonly int[] Slot2Tbl = { 3, 9, 4, 10, 5, 11, 15, 16, 17, 21, 27, 22, 28, 23, 29, 33, 34, 35 };
        private static readonly int[] ChTbl = { 0, 3, 1, 4, 2, 5, 6, 7, 8 };

        public Ymf278bVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;

            DrawBuffYmf278b.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYMF278B");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYMF278B.cs:104 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[][] ymf278bRegister = chipRegister.fmRegisterYMF278B[ChipID];
            if (ymf278bRegister == null) return;

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

                    nyc.inst[0 + i * 17] = ymf278bRegister[slotP][0x60 + slot] >> 4; // AR
                    nyc.inst[1 + i * 17] = ymf278bRegister[slotP][0x60 + slot] & 0xf; // DR
                    nyc.inst[2 + i * 17] = ymf278bRegister[slotP][0x80 + slot] >> 4; // SL
                    nyc.inst[3 + i * 17] = ymf278bRegister[slotP][0x80 + slot] & 0xf; // RR
                    nyc.inst[4 + i * 17] = ymf278bRegister[slotP][0x40 + slot] >> 6; // KL
                    nyc.inst[5 + i * 17] = ymf278bRegister[slotP][0x40 + slot] & 0x3f; // TL
                    nyc.inst[6 + i * 17] = ymf278bRegister[slotP][0x20 + slot] & 0xf; // MT
                    nyc.inst[7 + i * 17] = ymf278bRegister[slotP][0x20 + slot] >> 7; // AM
                    nyc.inst[8 + i * 17] = (ymf278bRegister[slotP][0x20 + slot] >> 6) & 1; // VB
                    nyc.inst[9 + i * 17] = (ymf278bRegister[slotP][0x20 + slot] >> 5) & 1; // EG
                    nyc.inst[10 + i * 17] = (ymf278bRegister[slotP][0x20 + slot] >> 4) & 1; // KR
                    nyc.inst[13 + i * 17] = ymf278bRegister[slotP][0xe0 + slot] & 7; // WS
                }
            }

            newParam.channels[18].dda = ((ymf278bRegister[1][0xbd] >> 7) & 0x01) != 0; // DA
            newParam.channels[19].dda = ((ymf278bRegister[1][0xbd] >> 6) & 0x01) != 0; // DV
            newParam.channels[20].freq = ymf278bRegister[2][0xf8] & 0x7; // FM MIX_L
            newParam.channels[21].freq = ymf278bRegister[2][0xf8] >> 3; // FM MIX_R
            newParam.channels[22].freq = ymf278bRegister[2][0xf9] & 0x7; // PCM MIX_L
            newParam.channels[23].freq = ymf278bRegister[2][0xf9] >> 3; // PCM MIX_R

            // ConnectSelect - pairs adjacent channels into 4-operator mode.
            for (int c = 0; c < 6; c++)
            {
                newParam.channels[c].dda = (ymf278bRegister[1][0x04] & (0x1 << c)) != 0;
                newParam.channels[c].inst[34] = newParam.channels[c].dda ? 1 : 0;
                newParam.channels[c].inst[35] = newParam.channels[c].dda ? 2 : 0;
                if (newParam.channels[c].dda)
                {
                    int ch = (c < 3) ? c * 2 : ((c - 3) * 2 + 9); // OP4 mode
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

            int ko = chipRegister.getYMF278BFMKeyON(ChipID);

            for (int c = 0; c < 18; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];

                int p = c / 9;
                int cadr = c % 9;
                int adr = ChTbl[cadr];

                nyc.inst[11] = (ymf278bRegister[p][0xb0 + adr] >> 2) & 7; // BL
                nyc.inst[12] = ymf278bRegister[p][0xa0 + adr] + ((ymf278bRegister[p][0xb0 + adr] & 3) << 8); // FNUM

                nyc.inst[15] = (ymf278bRegister[p][0xc0 + adr] >> 1) & 7; // FB
                nyc.inst[14] = ymf278bRegister[p][0xc0 + adr] & 1; // CN
                nyc.inst[36] = ymf278bRegister[p][0xc0 + adr] & 0x30; // PAN
                nyc.inst[36] = ((nyc.inst[36] >> 5) & 1) | ((nyc.inst[36] >> 3) & 2); // 00RL0000 -> 000000LR

                int n = ymf278bRegister[p][0xc0 + adr] & 1; // modFlg
                nyc.inst[16] = n == 0 ? 0 : 1;
                nyc.inst[33] = 1;

                int nt = Common.searchSegaPCMNote(nyc.inst[12] / 344.0) + (nyc.inst[11] - 4) * 12;

                bool fouropChannel = cadr < 6;
                bool fouropControl = fouropChannel && cadr % 2 == 0;

                // 4opのフラグはキーボードの並びと異なる (the 4-op flag doesn't line up with
                // keyboard channel order).
                MDChipParams.Channel? ccnt = fouropChannel ? newParam.channels[(p * 3) + (cadr / 2)] : null;
                MDChipParams.Channel? csub = fouropControl ? newParam.channels[c + 1] : null;
                bool fouropMode = ccnt != null && ccnt.dda;

                int cnt2 = fouropControl ? ymf278bRegister[p][0xc3 + adr] & 1 : 0;

                bool chmask = fouropMode && !fouropControl;

                if ((ko & (1 << (adr + p * 9))) != 0)
                {
                    if (nyc.note != nt && !chmask)
                    {
                        nyc.note = nt;

                        if (fouropMode && csub != null)
                        {
                            int tl1 = nyc.inst[5 + 0 * 17];
                            int tl2 = nyc.inst[5 + 1 * 17];
                            int tl3 = csub.inst[5 + 0 * 17];
                            int tl4 = csub.inst[5 + 1 * 17];

                            int tl = tl4; // cnt == 0 は TL4
                            int cnt = (n << 1) + cnt2;
                            switch (cnt)
                            {
                                case 1: tl = System.Math.Min(tl2, tl4); break;
                                case 2: tl = System.Math.Min(tl1, tl4); break;
                                case 3: tl = System.Math.Min(tl1, System.Math.Min(tl3, tl4)); break;
                            }

                            nyc.volumeL = (nyc.inst[36] & 2) != 0 ? (19 * (64 - tl) / 64) : 0;
                            nyc.volumeR = (nyc.inst[36] & 1) != 0 ? (19 * (64 - tl) / 64) : 0;
                        }
                        else
                        {
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
            int r = chipRegister.getYMF278BRyhthmKeyON(ChipID);

            if ((r & 0x10) != 0) // BD, slot16 TL 0x53
            {
                newParam.channels[18].volume = 19 - ((ymf278bRegister[0][0x53] & 0x3f) >> 2);
            }
            else
            {
                newParam.channels[18].volume--;
                if (newParam.channels[18].volume < 0) newParam.channels[18].volume = 0;
            }

            if ((r & 0x08) != 0) // SD, slot17 TL 0x54
            {
                newParam.channels[19].volume = 19 - ((ymf278bRegister[0][0x54] & 0x3f) >> 2);
            }
            else
            {
                newParam.channels[19].volume--;
                if (newParam.channels[19].volume < 0) newParam.channels[19].volume = 0;
            }

            if ((r & 0x04) != 0) // TOM, slot15 TL 0x52
            {
                newParam.channels[20].volume = 19 - ((ymf278bRegister[0][0x52] & 0x3f) >> 2);
            }
            else
            {
                newParam.channels[20].volume--;
                if (newParam.channels[20].volume < 0) newParam.channels[20].volume = 0;
            }

            if ((r & 0x02) != 0) // CYM, slot18 TL 0x55
            {
                newParam.channels[21].volume = 19 - ((ymf278bRegister[0][0x55] & 0x3f) >> 2);
            }
            else
            {
                newParam.channels[21].volume--;
                if (newParam.channels[21].volume < 0) newParam.channels[21].volume = 0;
            }

            if ((r & 0x01) != 0) // HH, slot14 TL 0x51
            {
                newParam.channels[22].volume = 19 - ((ymf278bRegister[0][0x51] & 0x3f) >> 2);
            }
            else
            {
                newParam.channels[22].volume--;
                if (newParam.channels[22].volume < 0) newParam.channels[22].volume = 0;
            }

            chipRegister.resetYMF278BRyhthmKeyON(ChipID);

            //PCM - this port never has a MoonDriver key-on source (see file header), so
            //always takes the "moonDriver以外" branch below.
            int[] pcmKey = chipRegister.getYMF278BPCMKeyON(ChipID);

            for (int c = 23; c < 23 + 24; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];
                int idx = c - 23;

                nyc.pan = ymf278bRegister[2][0x68 + idx] & 0xf;
                nyc.pan = nyc.pan == 8 ? 0 :
                    ((nyc.pan < 8 ? (15 - nyc.pan * 2) : 15)
                        + ((nyc.pan > 8 ? (nyc.pan * 2 - 18) : 15) << 4));

                nyc.inst[13] = ymf278bRegister[2][0x38 + idx] >> 4; // Oct
                nyc.inst[14] = (ymf278bRegister[2][0x20 + idx] >> 1) + ((ymf278bRegister[2][0x38 + idx] & 0x7) << 7); // F-Num

                if (pcmKey[idx] == 1)
                {
                    nyc.note = ((nyc.inst[13] + 7) & 0xf) * 12 + Common.searchPCMNote(nyc.inst[14], 1) - 5;
                    nyc.volumeL = (127 - (ymf278bRegister[2][0x50 + idx] >> 1)) * (nyc.pan & 0xf) / 16 / 6;
                    nyc.volumeR = (127 - (ymf278bRegister[2][0x50 + idx] >> 1)) * (nyc.pan >> 4) / 16 / 6;
                }
                else
                {
                    if (pcmKey[idx] == 2) nyc.note = -1;
                    nyc.volumeL--; if (nyc.volumeL < 0) nyc.volumeL = 0;
                    nyc.volumeR--; if (nyc.volumeR < 0) nyc.volumeR = 0;
                }

                nyc.inst[0] = ymf278bRegister[2][0x98 + idx] >> 4; // AR
                nyc.inst[1] = ymf278bRegister[2][0x98 + idx] & 0xf; // D1
                nyc.inst[2] = ymf278bRegister[2][0xb0 + idx] >> 4; // DL
                nyc.inst[3] = ymf278bRegister[2][0xb0 + idx] & 0xf; // D2
                nyc.inst[4] = ymf278bRegister[2][0xc8 + idx] >> 4; // RC
                nyc.inst[5] = ymf278bRegister[2][0xc8 + idx] & 0xf; // RR
                nyc.inst[6] = ymf278bRegister[2][0xe0 + idx] & 0x7; // AM
                nyc.inst[7] = ymf278bRegister[2][0x80 + idx] & 0x7; // Vib
                nyc.inst[8] = (ymf278bRegister[2][0x80 + idx] >> 3) & 0x7; // Lfo
                nyc.inst[9] = (ymf278bRegister[2][0x38 + idx] >> 3) & 0x1; // Reverb
                nyc.inst[10] = ymf278bRegister[2][0x50 + idx] & 0x1; // LD
                nyc.inst[11] = ymf278bRegister[2][0x50 + idx] >> 1; // TL
                nyc.inst[12] = ymf278bRegister[2][0x08 + idx] + ((ymf278bRegister[2][0x20 + idx] & 0x1) << 8); // Wav
            }

            chipRegister.resetYMF278BPCMKeyON(ChipID);
        }

        // frmYMF278B.cs:471 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 18; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                for (int i = 0; i < 2; i++)
                {
                    DrawBuffYmf278b.SusFlag(screen, 81 + i * 34, c * 2 + 2, 1, ref oyc.inst[16 + i * 17], nyc.inst[16 + i * 17]);
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 4 + i * 136, c * 8 + 8, ref oyc.inst[0 + i * 17], nyc.inst[0 + i * 17]); // AR
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 12 + i * 136, c * 8 + 8, ref oyc.inst[1 + i * 17], nyc.inst[1 + i * 17]); // DR
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 20 + i * 136, c * 8 + 8, ref oyc.inst[2 + i * 17], nyc.inst[2 + i * 17]); // SL
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 28 + i * 136, c * 8 + 8, ref oyc.inst[3 + i * 17], nyc.inst[3 + i * 17]); // RR

                    DrawBuffYmf278b.Font4Int2(screen, 336 + 40 + i * 136, c * 8 + 8, ref oyc.inst[4 + i * 17], nyc.inst[4 + i * 17]); // KL
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 48 + i * 136, c * 8 + 8, ref oyc.inst[5 + i * 17], nyc.inst[5 + i * 17]); // TL

                    DrawBuffYmf278b.Font4Int2(screen, 336 + 60 + i * 136, c * 8 + 8, ref oyc.inst[6 + i * 17], nyc.inst[6 + i * 17]); // MT

                    DrawBuffYmf278b.Font4Int2(screen, 336 + 72 + i * 136, c * 8 + 8, ref oyc.inst[7 + i * 17], nyc.inst[7 + i * 17]); // AM
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 80 + i * 136, c * 8 + 8, ref oyc.inst[8 + i * 17], nyc.inst[8 + i * 17]); // VB
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 88 + i * 136, c * 8 + 8, ref oyc.inst[9 + i * 17], nyc.inst[9 + i * 17]); // EG
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 96 + i * 136, c * 8 + 8, ref oyc.inst[10 + i * 17], nyc.inst[10 + i * 17]); // KR
                    DrawBuffYmf278b.Font4Int2(screen, 336 + 108 + i * 136, c * 8 + 8, ref oyc.inst[13 + i * 17], nyc.inst[13 + i * 17]); // WS
                }

                DrawBuffYmf278b.Font4Int2(screen, 336 + 4 * 65, c * 8 + 8, ref oyc.inst[11], nyc.inst[11]); // BL
                DrawBuffYmf278b.Font4Hex12Bit(screen, 336 + 4 * 69, c * 8 + 8, ref oyc.inst[12], nyc.inst[12]); // F-Num
                DrawBuffYmf278b.Font4Int2(screen, 336 + 4 * 73, c * 8 + 8, ref oyc.inst[14], nyc.inst[14]); // CN
                DrawBuffYmf278b.Font4Int2(screen, 336 + 4 * 76, c * 8 + 8, ref oyc.inst[15], nyc.inst[15]); // FB
                int dmy = 99;
                DrawBuffYmf278b.Pan(screen, 24, 8 + c * 8, ref oyc.inst[36], nyc.inst[36], ref dmy, 0);
                DrawBuffYmf278b.KeyBoard(screen, c, ref oyc.note, nyc.note);
                DrawBuffYmf278b.VolumeXY(screen, 64, c * 2 + 2, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYmf278b.VolumeXY(screen, 64, c * 2 + 3, 1, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYmf278b.ChYmf278b(screen, c, ref oyc.mask, nyc.mask);
            }

            for (int c = 0; c < 6; c++)
            {
                DrawBuffYmf278b.DrawNesSw(screen, 79 * 4 + c * 4, 19 * 8, ref oldParam.channels[c].dda, newParam.channels[c].dda); // CS
                DrawBuffYmf278b.Kakko(screen, 4 * 80, (c < 3 ? 0 : 24) + c * 16 + 8, 0, ref oldParam.channels[c].inst[34], newParam.channels[c].inst[34]);
                DrawBuffYmf278b.Kakko(screen, 4 * 162, (c < 3 ? 0 : 24) + c * 16 + 8, 0, ref oldParam.channels[c].inst[35], newParam.channels[c].inst[35]);
            }
            DrawBuffYmf278b.DrawNesSw(screen, 88 * 4, 19 * 8, ref oldParam.channels[18].dda, newParam.channels[18].dda); // DA
            DrawBuffYmf278b.DrawNesSw(screen, 92 * 4, 19 * 8, ref oldParam.channels[19].dda, newParam.channels[19].dda); // DV
            DrawBuffYmf278b.Font4Int1(screen, 109 * 4, 19 * 8, ref oldParam.channels[20].freq, newParam.channels[20].freq); // FM MIX_L
            DrawBuffYmf278b.Font4Int1(screen, 111 * 4, 19 * 8, ref oldParam.channels[21].freq, newParam.channels[21].freq); // FM MIX_R
            DrawBuffYmf278b.Font4Int1(screen, 100 * 4, 19 * 8, ref oldParam.channels[22].freq, newParam.channels[22].freq); // PCM MIX_L
            DrawBuffYmf278b.Font4Int1(screen, 102 * 4, 19 * 8, ref oldParam.channels[23].freq, newParam.channels[23].freq); // PCM MIX_R

            for (int c = 18; c < 23; c++)
            {
                DrawBuffYmf278b.ChYmf278b(screen, c, ref oldParam.channels[c].mask, newParam.channels[c].mask);
                DrawBuffYmf278b.VolumeXY(screen, 12 + (c - 18) * 13, 19 * 2, 0, ref oldParam.channels[c].volume, newParam.channels[c].volume);
            }

            //PCM
            for (int c = 23; c < 23 + 24; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffYmf278b.PanType2(screen, c - 4, ref oyc.pan, nyc.pan);
                DrawBuffYmf278b.Font4Int2(screen, 516 + 0, (c - 3) * 8, ref oyc.inst[0], nyc.inst[0]); // AR
                DrawBuffYmf278b.Font4Int2(screen, 516 + 8, (c - 3) * 8, ref oyc.inst[1], nyc.inst[1]); // D1
                DrawBuffYmf278b.Font4Int2(screen, 516 + 16, (c - 3) * 8, ref oyc.inst[2], nyc.inst[2]); // DL
                DrawBuffYmf278b.Font4Int2(screen, 516 + 24, (c - 3) * 8, ref oyc.inst[3], nyc.inst[3]); // D2
                DrawBuffYmf278b.Font4Int2(screen, 516 + 32, (c - 3) * 8, ref oyc.inst[4], nyc.inst[4]); // RC
                DrawBuffYmf278b.Font4Int2(screen, 516 + 40, (c - 3) * 8, ref oyc.inst[5], nyc.inst[5]); // RR
                DrawBuffYmf278b.Font4Int2(screen, 516 + 48, (c - 3) * 8, ref oyc.inst[6], nyc.inst[6]); // AM
                DrawBuffYmf278b.Font4Int2(screen, 516 + 56, (c - 3) * 8, ref oyc.inst[7], nyc.inst[7]); // VB
                DrawBuffYmf278b.Font4Int2(screen, 516 + 64, (c - 3) * 8, ref oyc.inst[8], nyc.inst[8]); // Lfo
                DrawBuffYmf278b.Font4Int2(screen, 516 + 72, (c - 3) * 8, ref oyc.inst[9], nyc.inst[9]); // RV
                DrawBuffYmf278b.Font4Int2(screen, 516 + 80, (c - 3) * 8, ref oyc.inst[10], nyc.inst[10]); // LD
                DrawBuffYmf278b.Font4Int2ThreeDigit(screen, 516 + 88, (c - 3) * 8, ref oyc.inst[11], nyc.inst[11]); // TL
                DrawBuffYmf278b.Font4Int2ThreeDigit(screen, 516 + 100, (c - 3) * 8, ref oyc.inst[12], nyc.inst[12]); // WV
                DrawBuffYmf278b.Font4Int2(screen, 516 + 112, (c - 3) * 8, ref oyc.inst[13], nyc.inst[13]); // Oct
                DrawBuffYmf278b.Font4Hex12Bit(screen, 516 + 128, (c - 3) * 8, ref oyc.inst[14], nyc.inst[14]); // F-Num

                DrawBuffYmf278b.ChYmf278b(screen, c, ref oyc.mask, nyc.mask);
                DrawBuffYmf278b.VolumeXY(screen, 113, (c - 3) * 2 + 0, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYmf278b.VolumeXY(screen, 113, (c - 3) * 2 + 1, 1, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYmf278b.KeyBoardToYmf278bPcm(screen, c - 4, ref oyc.note, nyc.note);
            }

            screen.Present();
        }
    }
}
