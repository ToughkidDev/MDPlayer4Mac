using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayer.Driver.GBS
{
    public class Inst
    {
        public Func<int> meth;
        public int length;
        public string flags;
        public int[] cycle;

        public Inst(Func<int> meth, int length, string cycle, string flags)
        {
            this.meth = meth;
            this.length = length;
            string scycle = cycle;
            this.flags = flags;

            string[] c = scycle.Split('/', StringSplitOptions.RemoveEmptyEntries);
            this.cycle = new int[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                if (!int.TryParse(c[i].Trim(), out this.cycle[i]))
                {
                    this.cycle[i] = 0;
                }
            }
        }
    }
}
