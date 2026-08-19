using MDPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayerx64.PianoRoll
{
    public class K051649(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {
        private List<int> crntReg;
        private List<int[]> freq;
        private List<int[]> vol;
        private List<int[]> keyonoff;
        private List<PrNote[]> Note;

        public override void Clear()
        {
            crntReg = [];
            freq = [];
            vol = [];
            keyonoff = [];
            Note = [];
            for (int i = 0; i < MAXChip; i++)
            {
                crntReg.Add(0);
                freq.Add(new int[5]);
                vol.Add(new int[5]);
                keyonoff.Add(new int[5]);
                Note.Add(new PrNote[5]);
            }
        }

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            switch (dAdr & 1)
            {
                case 0:
                    crntReg[chipID] = dData;
                    break;
                case 1:
                    int ch;
                    switch (dAdr >> 1)
                    {
                        case 1://freq
                            ch = crntReg[chipID] >> 1;
                            if (ch > 4) break;
                            if ((crntReg[chipID] & 1) != 0)
                                freq[chipID][ch] = (freq[chipID][ch] & 0xff) | (((byte)dData << 8) & 0xf00);
                            else
                                freq[chipID][ch] = (freq[chipID][ch] & 0xf00) | (byte)dData;
                            break;
                        case 2://volume
                            ch = crntReg[chipID] & 7;
                            if (ch > 4) break;
                            vol[chipID][ch] = dData & 0xf;
                            break;
                        case 3://keyonoff
                            for (int i = 0; i < 5; i++)
                            {
                                keyonoff[chipID][i] = dData & (1 << i);
                            }
                            break;
                        default:
                            break;
                    }
                    break;
            }

            for (int ch = 0; ch < 5; ch++)
            {
                float ftone = freq[chipID][ch] == 0 ? 0 : (Audio.ClockK051649 / (8.0f * (float)freq[chipID][ch]));
                int note = (95 - Common.searchSSGNote(ftone));

                if (keyonoff[chipID][ch]!=0 && vol[chipID][ch] != 0)
                {
                    //keyonした又はkeyon中
                    if (Note[chipID][ch] == null)
                    {
                        //keyONした！
                        Note[chipID][ch] = MakeNote(ch, vgmFrameCounter, note, freq[chipID][ch]);
                        lstPrNote.Add(Note[chipID][ch]);
                    }
                    else
                    {
                        //keyON中!
                        if (Note[chipID][ch].key != note)
                        {
                            //音程が異なる場合は新たなノートとする
                            Note[chipID][ch].endTick = vgmFrameCounter;
                            Note[chipID][ch] = MakeNote(ch, vgmFrameCounter, note, freq[chipID][ch]);
                            lstPrNote.Add(Note[chipID][ch]);
                        }
                    }
                }
                else
                {
                    //keyoffした又はkeyoff中

                    if (Note[chipID][ch] == null) continue;//keyoff中
                    
                    //keyOFFした！
                    Note[chipID][ch].endTick = vgmFrameCounter;
                    Note[chipID][ch] = null;

                }
            }
        }

        private static PrNote MakeNote(int ch, long startTick, int note, int freq)
        {
            PrNote ret = new()
            {
                ch = ch,
                startTick = startTick,
                endTick = -1,//長さ未確定
                key = note,
                freq = freq
            };

            ret.noteColor1[0] = 0x70;
            ret.noteColor1[1] = 0x70;
            ret.noteColor1[2] = 0x08;
            ret.noteColor1[3] = 0x90;
            ret.noteColor1[4] = 0x90;
            ret.noteColor1[5] = 0x08;

            ret.noteColor2[0] = 0x90;
            ret.noteColor2[1] = 0x90;
            ret.noteColor2[2] = 0x30;
            ret.noteColor2[3] = 0xb0;
            ret.noteColor2[4] = 0xb0;
            ret.noteColor2[5] = 0x50;

            return ret;
        }


    }
}
