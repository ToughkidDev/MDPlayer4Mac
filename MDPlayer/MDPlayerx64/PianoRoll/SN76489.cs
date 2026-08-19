using MDPlayer;

namespace MDPlayerx64.PianoRoll
{
    public class SN76489(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {
        private List<int> SN76489LatchedRegister;
        private List<int[]> SN76489Register;
        private List<int> SN76489NoiseFreq;
        private List<int[]> SN76489Vol;
        private List<PrNote[]> SN76489Note;

        public override void Clear()
        {
            SN76489Register = [];
            SN76489LatchedRegister = [];
            SN76489NoiseFreq = [];
            SN76489Note = [];
            SN76489Vol = [];

            for (int i = 0; i < MAXChip; i++)
            {
                SN76489Register.Add([0, 15, 0, 15, 0, 15, 0, 15]);
                SN76489LatchedRegister.Add(0);
                SN76489NoiseFreq.Add(0);
                SN76489Note.Add(new PrNote[4]);
                SN76489Vol.Add([0, 0, 0, 0]);
            }
        }

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            if ((dData & 0x80) != 0)
            {
                /* Latch/data byte  %1 cc t dddd */
                SN76489LatchedRegister[chipID] = (dData >> 4) & 0x07;
                SN76489Register[chipID][SN76489LatchedRegister[chipID]] =
                    (SN76489Register[chipID][SN76489LatchedRegister[chipID]] & 0x3f0) /* zero low 4 bits */
                    | (dData & 0xf);                            /* and replace with data */
            }
            else
            {
                /* Data byte        %0 - dddddd */
                if ((SN76489LatchedRegister[chipID] % 2) == 0 && (SN76489LatchedRegister[chipID] < 5))
                    /* Tone register */
                    SN76489Register[chipID][SN76489LatchedRegister[chipID]] =
                        (SN76489Register[chipID][SN76489LatchedRegister[chipID]] & 0x00f) /* zero high 6 bits */
                        | ((dData & 0x3f) << 4);                 /* and replace with data */
                else
                    /* Other register */
                    SN76489Register[chipID][SN76489LatchedRegister[chipID]] = dData & 0x0f; /* Replace with data */
            }
            switch (SN76489LatchedRegister[chipID])
            {
                case 0:
                case 2:
                case 4: /* Tone channels */
                    //if (sn76489Register[chipID][LatchedRegister[chipID]] == 0)
                    //sn76489Register[chipID][LatchedRegister[chipID]] = 1; /* Zero frequency changed to 1 to avoid div/0 */
                    break;
                case 6: /* Noise */
                    SN76489NoiseFreq[chipID] = 0x10 << (SN76489Register[chipID][6] & 0x3); /* set noise signal generator frequency */
                    break;
            }
            if ((dData & 0x10) != 0)
            {
                if (SN76489LatchedRegister[chipID] != 0 && SN76489LatchedRegister[chipID] != 2 && SN76489LatchedRegister[chipID] != 4 && SN76489LatchedRegister[chipID] != 6)
                {
                    SN76489Vol[chipID][(dData & 0x60) >> 5] = (15 - (dData & 0xf));
                }
            }

            //Tone Ch
            for (int ch = 0; ch < 3; ch++)
            {
                int freq = SN76489Register[chipID][ch * 2];
                int note;
                if (SN76489Register[chipID][ch * 2 + 1] != 15 && freq != 0)
                {
                    float ftone = Audio.ClockSN76489 / (2.0f * SN76489Register[chipID][ch * 2] * 16.0f);
                    note = SearchSSGNote(ftone);// searchPSGNote(psgRegister[ch * 2]);
                }
                else
                {
                    note = -1;
                }

                if (note != -1)
                {
                    if (SN76489Note[chipID][ch] == null)
                    {
                        //keyONした！
                        SN76489Note[chipID][ch] = SN76489MakeNote(ch, vgmFrameCounter, note, freq);
                        lstPrNote.Add(SN76489Note[chipID][ch]);
                    }
                    else
                    {
                        //keyON中!
                        if (SN76489Note[chipID][ch].key != note)
                        {
                            //音程が異なる場合は新たなノートとする
                            SN76489Note[chipID][ch].endTick = vgmFrameCounter;
                            SN76489Note[chipID][ch] = SN76489MakeNote(ch, vgmFrameCounter, note, freq);
                            lstPrNote.Add(SN76489Note[chipID][ch]);
                        }
                    }
                }
                else
                {
                    //keyOFF
                    if (SN76489Note[chipID][ch] != null)
                    {
                        //keyOFFした！
                        SN76489Note[chipID][ch].endTick = vgmFrameCounter;
                        SN76489Note[chipID][ch] = null;
                    }
                    else
                    {
                        //keyOFF中
                    }

                }
            }

        }
        private static int SearchSSGNote(float freq)
        {
            float m = float.MaxValue;
            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                float a = Math.Abs(freq - Tables.freqTbl[i]);
                if (m > a)
                {
                    m = a;
                    n = i;
                }
            }
            return (95 - n);
        }

        private static PrNote SN76489MakeNote(int ch, long startTick, int note, int freq)
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
