using MDPlayer;
using NAudio.Gui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayerx64.PianoRoll
{
    public class YM2413(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {
        private List<byte[]> reg;
        private List<bool[]> KeyOn;
        private List<bool[]> KeyOnOld;
        private List<int[]> Fnum;
        private List<int[]> FnumOld;
        private List<PrNote[]> Note;

        public override void Clear()
        {
            reg = [];
            KeyOn = [];
            KeyOnOld = [];
            Fnum = [];
            FnumOld = [];
            Note = [];
            for (int i = 0; i < MAXChip; i++)
            {
                reg.Add(new byte[0x100]);
                KeyOn.Add([false, false, false, false, false, false, false, false, false]);
                KeyOnOld.Add([false, false, false, false, false, false, false, false, false]);
                Fnum.Add([-1, -1, -1, -1, -1, -1, -1, -1, -1]);
                FnumOld.Add([-1, -1, -1, -1, -1, -1, -1, -1, -1]);
                Note.Add(new PrNote[9]);
            }
        }

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            reg[chipID][dAdr] = (byte)dData;
            if (dAdr < 0x10 || (dAdr >= 0x19 && dAdr < 0x20) || dAdr >= 0x29) return;

            int ch = dAdr & 0xf;
            int freq = reg[chipID][0x10 + ch] + ((reg[chipID][0x20 + ch] & 0x1) << 8);
            int oct = ((reg[chipID][0x20 + ch] & 0xe) >> 1);
            int fnum = Common.searchSegaPCMNote(freq / 172.0) + (oct - 4) * 12;
            int k = reg[chipID][0x20 + ch] & 0x10;
            KeyOn[chipID][ch] = k != 0;
            Fnum[chipID][ch] = fnum;

            if (KeyOnOld[chipID][ch] == KeyOn[chipID][ch]
                && FnumOld[chipID][ch] == Fnum[chipID][ch]
                ) return;
            KeyOnOld[chipID][ch] = KeyOn[chipID][ch];
            FnumOld[chipID][ch] = Fnum[chipID][ch];

            if (!KeyOn[chipID][ch])
            {
                //keyOff
                if (Note[chipID][ch] != null)
                    Note[chipID][ch].endTick = vgmFrameCounter;
                return;
            }

            //keyOn
            if (Note[chipID][ch] != null && Note[chipID][ch].endTick == -1)
                Note[chipID][ch].endTick = vgmFrameCounter;

            Note[chipID][ch] = MakeNote(ch, vgmFrameCounter, fnum, freq);
            lstPrNote.Add(Note[chipID][ch]);

        }

        private static PrNote MakeNote(int ch, long startTick, int ff, int freq)
        {
            PrNote ret = new PrNote
            {
                ch = ch,
                startTick = startTick,
                endTick = -1,//長さ未確定
                key = 95 - ff,
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

    }
}
