using MDPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayerx64.PianoRoll
{
    public class YM2151(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {

        private List<byte[]> reg;
        private List<int[]> KeyOn;
        private List<int[]> KeyOnOld;
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
                KeyOn.Add(new int[8]);
                KeyOnOld.Add(new int[8]);
                Fnum.Add(new int[8]);
                FnumOld.Add(new int[8]);
                Note.Add(new PrNote[8]);
            }
        }

        private static float[] fmDivTbl = new float[] { 6, 3, 2 };

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            if (reg == null) return;
            reg[chipID][dAdr] = (byte)dData;

            //FM Ch1-8

            //keyon/offチェック
            if (dAdr == 0x08)
            {
                int ch = dData & 0x7;
                KeyOn[chipID][ch] = dData & 0x78;
            }

            int hosei = 0;
            if (Audio.DriverVirtual != null)//is vgm)
            {
                hosei = (Audio.DriverVirtual).YM2151Hosei[chipID];
            }

            for (int ch = 0; ch < 8; ch++)
            {
                //fnumチェック
                int note = (reg[chipID][0x28 + ch] & 0x0f);
                note = (note < 3) ? note : (note < 7 ? note - 1 : (note < 11 ? note - 2 : note - 3));
                int oct = ((reg[chipID][0x28 + ch] & 0x70) >> 4);
                note = KeyOn[chipID][ch] != 0 ? (oct * 12 + note + hosei) : -1;
                Fnum[chipID][ch] = note;
            }

            for (int ch = 0; ch < 8; ch++)
            {
                if (!(
                    KeyOnOld[chipID][ch] != KeyOn[chipID][ch]
                    || (
                        (KeyOn[chipID][ch] & 0xf) != 0 && FnumOld[chipID][ch] != Fnum[chipID][ch]
                       )
                   ))
                    continue;

                KeyOnOld[chipID][ch] = KeyOn[chipID][ch];
                FnumOld[chipID][ch] = Fnum[chipID][ch];

                if (KeyOn[chipID][ch] == 0)
                {
                    //keyOff
                    if (Note[chipID][ch] != null)
                        Note[chipID][ch].endTick = vgmFrameCounter;
                    continue;
                }

                //keyOn
                if (Note[chipID][ch] != null && Note[chipID][ch].endTick == -1)
                    Note[chipID][ch].endTick = vgmFrameCounter;

                Note[chipID][ch] = MakeFMNote(ch, vgmFrameCounter, Fnum[chipID][ch]);
                lstPrNote.Add(Note[chipID][ch]);
                continue;

            }


        }

        private static PrNote MakeFMNote(int ch, long startTick, int fnum)
        {
            PrNote ret = new PrNote
            {
                ch = ch,
                startTick = startTick,
                endTick = -1,//長さ未確定
                key = 95 - fnum
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
