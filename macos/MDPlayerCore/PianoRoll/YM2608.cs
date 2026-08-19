using MDPlayer;

namespace MDPlayerx64.PianoRoll
{
    public class YM2608(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {

        private List<byte[]> reg;
        private List<int[]> KeyOn;
        private List<int> KeyOnCh3;
        private List<int[]> KeyOnOld;
        private List<int> KeyOnCh3Old;
        private List<int[]> Fnum;
        private List<int[]> FnumOld;
        private List<int[]> FnumCh3;
        private List<int[]> FnumCh3Old;
        private List<bool> Ch3ExFlg;
        private List<bool> Ch3ExFlgOld;
        private List<PrNote[]> Note;
        private List<PrNote[]> NoteCh3;
        private List<PrNote[]> SSGNote;

        public override void Clear()
        {
            reg = [];
            KeyOn = [];
            KeyOnCh3 = [];
            KeyOnOld = [];
            KeyOnCh3Old = [];
            Fnum = [];
            FnumOld = [];
            FnumCh3 = [];
            FnumCh3Old = [];
            Note = [];
            NoteCh3 = [];
            Ch3ExFlg = [];
            Ch3ExFlgOld = [];
            SSGNote = [];
            for (int i = 0; i < MAXChip; i++)
            {
                reg.Add(new byte[0x200]);
                KeyOn.Add(new int[6]);
                KeyOnCh3.Add(0);
                KeyOnOld.Add(new int[6]);
                KeyOnCh3Old.Add(0);
                Fnum.Add(new int[6]);
                FnumOld.Add(new int[6]);
                FnumCh3.Add(new int[4]);
                FnumCh3Old.Add(new int[4]);
                Note.Add(new PrNote[6]);
                NoteCh3.Add(new PrNote[4]);
                Ch3ExFlg.Add(false);
                Ch3ExFlgOld.Add(false);
                SSGNote.Add(new PrNote[3]);
            }
        }

        private static float[] fmDivTbl = new float[] { 6, 3, 2 };
        private static float[] ssgDivTbl = new float[] { 4, 2, 1 };

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            if (reg == null) return;
            reg[chipID][dAdr] = (byte)dData;

            //FM Ch1-6 Ch3EX
            //keyon/offチェック
            if (dAdr == 0x100 * 0 + 0x28)
            {
                int ch = (dData & 0x3) + ((dData & 0x4) > 0 ? 3 : 0);
                if (ch >= 0 && ch < 6)// && (dData & 0xf0)>0)
                {
                    if (ch != 2 || (reg[chipID][0x000 * 0 + 0x27] & 0xc0) != 0x40)
                    {
                        if (ch != 5 || (reg[chipID][0x000 * 0 + 0x2b] & 0x80) == 0)
                        {
                            if ((dData & 0xf0) != 0)
                                KeyOn[chipID][ch] = (dData & 0xf0) | 1;
                            else
                                KeyOn[chipID][ch] = (dData & 0xf0) | 0;
                        }
                    }
                    else
                    {
                        KeyOn[chipID][2] = (dData & 0xf0);
                        for (int i = 0; i < 4; i++)
                        {
                            int b = 0x10 << i;
                            if ((KeyOnCh3[chipID] & b) != (dData & b))
                                KeyOnCh3[chipID] = (KeyOnCh3[chipID] & ~b) | (dData & b);
                        }
                    }
                }
            }

            //fnumチェック
            if ((dAdr >= 0xa0 && dAdr <= 0xa6) || (dAdr >= 0x1a0 && dAdr <= 0x1a6))
            {
                for (int i = 0; i < 6; i++)
                {
                    int p = (i > 2) ? 0x100 : 0;
                    int c = (i > 2) ? i - 3 : i;
                    int freq = reg[chipID][p + 0xa0 + c] + (reg[chipID][p + 0xa4 + c] & 0x3f) * 0x100;
                    Fnum[chipID][i] = freq;
                }
                for (int i = 0; i < 4; i++)
                {
                    int c = (i == 0) ? (-6) : (i - 1);
                    int freq = reg[chipID][0xa8 + c] + (reg[chipID][0xac + c] & 0x3f) * 0x100;
                    FnumCh3[chipID][i] = freq;
                }
            }

            //Ch3ExFlgチェック
            if (dAdr == 0x27)
            {
                Ch3ExFlg[chipID] = (dData & 0xc0) != 0;
            }

            int masterClock = 7987200;// defaultMasterClock
            float ssgMul = 1.0f;
            if (Audio.ClockYM2608 != 0)
            {
                ssgMul = Audio.ClockYM2608 / (float)masterClock;
                masterClock = Audio.ClockYM2608;
            }
            int divInd = reg[chipID][0x2d];
            if (divInd < 0 || divInd > 2) divInd = 0;
            float fmDiv = fmDivTbl[divInd];
            float ssgDiv = ssgDivTbl[divInd];
            ssgMul = ssgMul / ssgDiv * 4;

            for (int i = 0; i < 6; i++)
            {
                if (i != 2 || (reg[chipID][0x000 * 0 + 0x27] & 0xc0) != 0x40)
                {
                    if (!(
                        KeyOnOld[chipID][i] != KeyOn[chipID][i]
                        || (
                            (KeyOn[chipID][i] & 0xf) != 0 && FnumOld[chipID][i] != Fnum[chipID][i]
                           )
                       ))
                        continue;

                    KeyOnOld[chipID][i] = KeyOn[chipID][i];
                    FnumOld[chipID][i] = Fnum[chipID][i];

                    if ((KeyOn[chipID][i] & 0xf) == 0)
                    {
                        //keyOff
                        if (Note[chipID][i] != null)
                            Note[chipID][i].endTick = vgmFrameCounter;
                        continue;
                    }

                    //keyOn
                    if (Note[chipID][i] != null && Note[chipID][i].endTick == -1)
                        Note[chipID][i].endTick = vgmFrameCounter;

                    int p = (i > 2) ? 0x100 : 0;
                    int c = (i > 2) ? i - 3 : i;
                    int freq = reg[chipID][p + 0xa0 + c] + (reg[chipID][p + 0xa4 + c] & 0x07) * 0x100;
                    int octav = (reg[chipID][p + 0xa4 + c] & 0x38) >> 3;
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    Note[chipID][i] = MakeFMNote(i, vgmFrameCounter, ff, freq);
                    lstPrNote.Add(Note[chipID][i]);
                    continue;
                }

                //Ch3Ex
                for (int j = 0; j < 4; j++)
                {
                    int b = 0x10 << j;
                    if (
                        !(
                            (KeyOnCh3Old[chipID] & b) != (KeyOnCh3[chipID] & b)
                            || (
                                (KeyOnCh3[chipID] & b) != 0
                                && FnumCh3Old[chipID][j] != FnumCh3[chipID][j])
                        )) continue;

                    KeyOnCh3Old[chipID] = (KeyOnCh3Old[chipID] & ~b) | (KeyOnCh3[chipID] & b);
                    FnumCh3Old[chipID][j] = FnumCh3[chipID][j];
                    if ((KeyOn[chipID][j] & b) == 0)
                    {
                        //keyOff
                        if (NoteCh3[chipID][j] != null)
                            NoteCh3[chipID][j].endTick = vgmFrameCounter;
                        continue;
                    }

                    //keyOn
                    if (NoteCh3[chipID][j] != null && NoteCh3[chipID][j].endTick == -1)
                        NoteCh3[chipID][j].endTick = vgmFrameCounter;

                    int c = (j == 0) ? (-6) : (j - 1);
                    int freq = reg[chipID][0xa8 + c] + (reg[chipID][0xac + c] & 0x07) * 0x100;
                    int octav = (reg[chipID][0xac + c] & 0x38) >> 3;
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    NoteCh3[chipID][j] = MakeFMNote(j, vgmFrameCounter, ff, freq);
                    lstPrNote.Add(NoteCh3[chipID][j]);
                }
            }

            if (Ch3ExFlgOld[chipID] != Ch3ExFlg[chipID])
            {
                Ch3ExFlgOld[chipID] = Ch3ExFlg[chipID];
                for (int i = 0; i < 4; i++)
                {
                    if (NoteCh3[chipID][i] == null || NoteCh3[chipID][i].endTick != -1) continue;
                    NoteCh3[chipID][i].endTick = vgmFrameCounter;
                }
                if (Note[chipID][2] != null && Note[chipID][2].endTick == -1)
                    Note[chipID][2].endTick = vgmFrameCounter;

            }

            //SSG Ch1-3
            for (int ch = 0; ch < 3; ch++)
            {
                int ft = reg[chipID][0x00 + ch * 2];
                int ct = reg[chipID][0x01 + ch * 2] & 0xf;
                int freq = (ct << 8) | ft;
                int volume = reg[chipID][0x08 + ch] & 0xf;

                int note;
                if (volume == 0 && (reg[chipID][0x08 + ch] & 0x10) == 0)
                {
                    note = -1;
                }
                else
                {
                    if (freq == 0)
                    {
                        note = -1;
                    }
                    else
                    {
                        float ftone = masterClock / (64.0f * (float)freq) * ssgMul;// 7987200 = MasterClock
                        note = (95-Common.searchSSGNote(ftone));
                    }
                }

                if (note != -1)
                {
                    if (SSGNote[chipID][ch] == null)
                    {
                        //keyONした！
                        SSGNote[chipID][ch] = MakeSSGNote(ch, vgmFrameCounter, note, freq);
                        lstPrNote.Add(SSGNote[chipID][ch]);
                    }
                    else
                    {
                        //keyON中!
                        if (SSGNote[chipID][ch].key != note)
                        {
                            //音程が異なる場合は新たなノートとする
                            SSGNote[chipID][ch].endTick = vgmFrameCounter;
                            SSGNote[chipID][ch] = MakeSSGNote(ch, vgmFrameCounter, note, freq);
                            lstPrNote.Add(SSGNote[chipID][ch]);
                        }
                    }
                }
                else
                {
                    //keyOFF
                    if (SSGNote[chipID][ch] != null)
                    {
                        //keyOFFした！
                        SSGNote[chipID][ch].endTick = vgmFrameCounter;
                        SSGNote[chipID][ch] = null;
                    }
                    else
                    {
                        //keyOFF中
                    }

                }

            }
        }

        private static PrNote MakeFMNote(int ch, long startTick, float ff, int freq)
        {
            PrNote ret = new PrNote
            {
                ch = ch,
                startTick = startTick,
                endTick = -1,//長さ未確定
                key = 95 - Math.Min(Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95),
                freq = freq
            };
            ret.noteColor1[0] = 0xd0;
            ret.noteColor1[1] = 0x00;
            ret.noteColor1[2] = 0x50;
            ret.noteColor1[3] = 0xf0;
            ret.noteColor1[4] = 0x00;
            ret.noteColor1[5] = 0x70;
            ret.noteColor2[0] = 0x90;
            ret.noteColor2[1] = 0x00;
            ret.noteColor2[2] = 0x90;
            ret.noteColor2[3] = 0xd0;
            ret.noteColor2[4] = 0x00;
            ret.noteColor2[5] = 0xb0;

            return ret;
        }


        private static PrNote MakeSSGNote(int ch, long startTick, int note, int freq)
        {
            PrNote ret = new()
            {
                ch = ch,
                startTick = startTick,
                endTick = -1,//長さ未確定
                key = note,
                freq = freq
            };

            ret.noteColor1[0] = 0x00;
            ret.noteColor1[1] = 0x50;
            ret.noteColor1[2] = 0x08;
            ret.noteColor1[3] = 0x00;
            ret.noteColor1[4] = 0x70;
            ret.noteColor1[5] = 0x08;

            ret.noteColor2[0] = 0x00;
            ret.noteColor2[1] = 0x80;
            ret.noteColor2[2] = 0x30;
            ret.noteColor2[3] = 0x00;
            ret.noteColor2[4] = 0xa0;
            ret.noteColor2[5] = 0x50;

            return ret;
        }

    }
}