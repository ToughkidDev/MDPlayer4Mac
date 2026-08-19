using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayer.Driver.ZMS.nise68
{
    public class memMng
    {
        public Dictionary<uint, uint> dicMng = new Dictionary<uint, uint>();
        private uint startPtr = 0;
        public uint Address { get; internal set; } = 0x2000;
        public int allocCount { 
            get { 
                return dicMng.Count;
            }
            set { }
        }



        public memMng(uint startAdr)
        {
            startPtr = startAdr;
            dicMng.Clear();
            allocCount = 0;
            Address = 0x2000;
        }



        int bl = 4;
        public bool Set(uint memPtr, uint size)
        {
            if (dicMng.ContainsKey(memPtr)) return false;

            dicMng.Add(memPtr, size);
            uint m = memPtr + size;
            startPtr = Math.Max(startPtr, m);
            if (startPtr % bl != 0) startPtr += (uint)(bl - (startPtr % bl));
            return true;
        }

        public bool Change(uint newptr, uint newlen)
        {
            if (!dicMng.ContainsKey(newptr)) return false;
            dicMng[newptr] = newlen;
            uint m = newptr + newlen;
            startPtr = Math.Max(startPtr, m);
            if (startPtr % bl != 0) startPtr += (uint)(bl - (startPtr % bl));
            return true;
        }

        public int Malloc(uint bytesize)
        {
            uint ret = startPtr;
            dicMng.Add(startPtr, bytesize);
            startPtr += bytesize;
            if(startPtr% bl != 0) startPtr += (uint)(bl - (startPtr % bl));
            return (int)ret;
        }

        public int Mfree(uint ptr)
        {
            if (!dicMng.ContainsKey(ptr))
                return -1;
            //dicMng.Remove(ptr);
            return 0;
        }
    }
}
