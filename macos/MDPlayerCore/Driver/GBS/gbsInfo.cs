using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayer
{
    public class gbsInfo
    {
        public byte version;
        public byte nums;
        public byte firstSong;
        public ushort loadAddress;
        public ushort initAddress;
        public ushort playAddress;
        public ushort sp;
        public byte timerModulo;
        public byte timerControl;
        public string Title;
        public string Author;
        public string Copyright;
        public byte[][] mem;
    }

}
