using MDPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MDPlayerx64.PianoRoll
{
    public class HuC6280(List<PrNote> lstPrNote, int MAXChip = 2) : BaseChip(lstPrNote, MAXChip)
    {
        private List<uint> crntChannel;
        private List<uint[]> frq;
        private List<uint[]> volume;
        private List<uint[]> volumeL;
        private List<uint[]> volumeR;
        private List<uint[]> outVolumeL;
        private List<uint[]> outVolumeR;
        private List<PrNote[]> Note;

        public override void Clear()
        {
            crntChannel = [];
            frq = [];
            volume = [];
            volumeL = [];
            volumeR = [];
            outVolumeL = [];
            outVolumeR = [];
            Note = [];

            for (int i = 0; i < MAXChip; i++)
            {
                crntChannel.Add(0);
                frq.Add([0, 0, 0, 0, 0, 0]);
                volume.Add([0, 0, 0, 0, 0, 0]);
                volumeL.Add([0, 0, 0, 0, 0, 0]);
                volumeR.Add([0, 0, 0, 0, 0, 0]);
                outVolumeL.Add([0, 0, 0, 0, 0, 0]);
                outVolumeR.Add([0, 0, 0, 0, 0, 0]);
                Note.Add(new PrNote[6]);
            }
        }

        public override void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter)
        {
            switch (dAdr & 15)
            {
                case 0: // register select
                    crntChannel[chipID] = (uint)(dData & 7);
                    break;

                case 1: // main volume
                    //MainVolumeL = (uint)((dData >> 4) & 0x0F);
                    //MainVolumeR = (uint)(dData & 0x0F);
                    break;

                case 2: // frequency low
                    frq[chipID][crntChannel[chipID]] &= ~(uint)0xFF;
                    frq[chipID][crntChannel[chipID]] |= (uint)dData;
                    break;

                case 3: // frequency high
                    frq[chipID][crntChannel[chipID]] &= ~(uint)0xF00;
                    frq[chipID][crntChannel[chipID]] |= (uint)((dData & 0x0F) << 8);
                    break;

                case 4: // ON, DDA, AL
                    volume[chipID][crntChannel[chipID]] = (uint)(dData & 0x1F);
                    break;

                case 5: // LAL, RAL
                    volumeL[chipID][crntChannel[chipID]] = (uint)((dData >> 4) & 0xF);
                    volumeR[chipID][crntChannel[chipID]] = (uint)(dData & 0xF);
                    break;

                case 6: // wave data
                    break;
                case 7: // noise on, noise frq
                    break;
                case 8: // LFO frequency
                    break;
                case 9: // LFO control
                    break;
                default:    // invalid write
                    break;
            }


            for (int ch = 0; ch < 6; ch++)
            {
                uint outVolumeL = volume[chipID][ch] * volumeL[chipID][ch];
                uint outVolumeR = volume[chipID][ch] * volumeR[chipID][ch];

                int tp = (int)frq[chipID][ch];
                if (tp == 0) tp = 1;
                float ftone = 3579545.0f / 32.0f / (float)tp;
                int note = (95-Common.searchSSGNote(ftone));
                if (outVolumeL == 0 && outVolumeR == 0) note = -1;

                if (note != -1)
                {
                    if (Note[chipID][ch] == null)
                    {
                        //keyONした！
                        Note[chipID][ch] = MakeNote(ch, vgmFrameCounter, note, (int)ftone);
                        lstPrNote.Add(Note[chipID][ch]);
                    }
                    else
                    {
                        //keyON中!
                        if (Note[chipID][ch].key != note)
                        {
                            //音程が異なる場合は新たなノートとする
                            Note[chipID][ch].endTick = vgmFrameCounter;
                            Note[chipID][ch] = MakeNote(ch, vgmFrameCounter, note, (int)ftone);
                            lstPrNote.Add(Note[chipID][ch]);
                        }
                    }
                    continue;
                }

                //keyOFF
                if (Note[chipID][ch] == null) continue;

                //keyOFFした！
                Note[chipID][ch].endTick = vgmFrameCounter;
                Note[chipID][ch] = null;

                //keyOFF中は何もしない

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

            ret.noteColor1[0] = 0x40;
            ret.noteColor1[1] = 0x00;
            ret.noteColor1[2] = 0x78;
            ret.noteColor1[3] = 0x60;
            ret.noteColor1[4] = 0x00;
            ret.noteColor1[5] = 0x98;

            ret.noteColor2[0] = 0x70;
            ret.noteColor2[1] = 0x30;
            ret.noteColor2[2] = 0xA0;
            ret.noteColor2[3] = 0x90;
            ret.noteColor2[4] = 0x30;
            ret.noteColor2[5] = 0xC0;

            return ret;
        }

    }
}
