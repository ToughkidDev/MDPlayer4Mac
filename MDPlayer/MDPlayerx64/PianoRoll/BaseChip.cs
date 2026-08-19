using MDPlayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayerx64.PianoRoll
{
    abstract public class BaseChip(List<PrNote> lstPrNote, int MAXChip = 2)
    {
        protected List<PrNote> lstPrNote = lstPrNote;
        protected int MAXChip = MAXChip;

        abstract public void Clear();
        abstract public void Analyze(int chipID, int dAdr, int dData, long vgmFrameCounter);
    }
}
