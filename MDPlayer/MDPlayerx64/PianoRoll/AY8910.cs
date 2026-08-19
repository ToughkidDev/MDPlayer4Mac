using MDPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayerx64.PianoRoll
{
    public class AY8910(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {
        private List<byte[]> reg;
        private List<PrNote[]> SSGNote;

        public override void Clear()
        {
            reg = [];
            SSGNote = [];
            for (int i = 0; i < MAXChip; i++)
            {
                reg.Add(new byte[0x100]);
                SSGNote.Add(new PrNote[3]);
            }
        }

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            if (reg == null) return;
            reg[chipID][dAdr] = (byte)dData;

            //SSG Ch1-3
            for (int ch = 0; ch < 3; ch++)
            {
                int ft = reg[chipID][0x00 + ch * 2];
                int ct = reg[chipID][0x01 + ch * 2] & 0xf;
                int freq = (ct << 8) | ft;
                int volume = reg[chipID][0x08 + ch] & 0xf;

                int note = -1;
                if (volume != 0 || (reg[chipID][0x08 + ch] & 0x10) != 0)
                {
                    if (freq != 0)
                    {
                        float ftone = Audio.ClockAY8910 / (8.0f * (float)freq);
                        note = (95 - Common.searchSSGNote(ftone));
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
                    continue;
                }

                //keyOFF
                if (SSGNote[chipID][ch] == null) continue;

                //keyOFFした！
                SSGNote[chipID][ch].endTick = vgmFrameCounter;
                SSGNote[chipID][ch] = null;

                //keyOFF中は何もしない

            }
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
