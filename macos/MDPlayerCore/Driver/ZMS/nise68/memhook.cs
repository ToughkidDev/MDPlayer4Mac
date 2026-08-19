
namespace MDPlayer.Driver.ZMS.nise68
{
    public class memhook
    {
        public uint startAdr;
        public uint endAdr;
        public Func<uint, uint> read;
        public Func<uint, byte, bool> write;

        public memhook(uint startAdr, uint endAdr, Func<uint,uint> read, Func<uint, byte, bool> write)
        {
            this.startAdr = startAdr;
            this.endAdr = endAdr;
            this.read = read;
            this.write = write;
        }

        public byte ReadB(uint adr)
        {
            return (byte)read(adr);
        }

        public ushort ReadW(uint adr)
        {
            return (ushort)read(adr);
        }
        public uint ReadL(uint adr)
        {
            return (uint)read(adr);
        }

        public bool WriteB(uint adr, byte dat)
        {
            return write(adr, dat);
        }
    }
}