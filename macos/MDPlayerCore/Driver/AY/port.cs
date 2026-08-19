using Konamiman.Z80dotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MDPlayer.Driver.AY
{
    public class port : IMemory
    {
        public IZ80Registers registers;
        public ChipRegister chipRegister;
        public EnmModel model;
        public AY CPU;

        private byte AYReg = 0;
        private byte AYDat = 0;
        private byte[] AYRegMap=new byte[255];
        private byte CPCSw = 0;
        private int BN = 0;
        private int BP = 0;
        private baseDriver driver;

        public port(baseDriver driver)
        {
            this.driver = driver;
        }

        public byte this[int address]
        {
            get
            {
                return InPort(address);
            }

            set
            {
                OutPort(address, value);
            }
        }

        public int Size => throw new NotImplementedException();

        public byte[] GetContents(int startAddress, int length)
        {
            throw new NotImplementedException();
        }

        public void SetContents(int startAddress, byte[] contents, int startIndex = 0, int? length = null)
        {
            throw new NotImplementedException();
        }


        private void OutPort(int address, byte value)
        {
            //CPC
            if (registers.B == 0xf4 || registers.B == 0xf6)
            {
                CPU.SetCPCclock();
                CPCOutPort(value);
                return;
            }

            //ZX

            CPU.SetZXclock();
            address = registers.B * 0x100 | (byte)address;

            if ((address & 0xc002) == 0xc000)
            {
                AYReg = value;
            }
            else if ((address & 0xc002) == 0x8000)
            {
                AYDat = value;
                chipRegister.setAY8910Register(0, AYReg, AYDat, model, driver.vgmFrameCounter);
                AYRegMap[AYReg] = AYDat;
                //Debug.WriteLine("AY Reg:{0:x02} Dat:{1:x02}", AYReg, AYDat);
            }
            else if ((address & 0x0001) == 0)
            {
                if ((value & 16) != 0)
                    BN = 10;
                else
                    BN = 0;
                if (BN != BP)
                {
                    chipRegister.setZXBeep(0, model);
                    BP = BN;
                }
            }
            else
            {
                Debug.WriteLine("Out Port Adr:{0:x04} Dat:{1:x02}", address, value);
            }
        }

        private void CPCOutPort(byte value)
        {
            if (registers.B == 0xf4)
            {
                AYDat = value;
                return;
            }

            if (registers.B != 0xf6) return;

            byte b = (byte)(value & 0xc0);

            if (CPCSw == 0)
            {
                CPCSw = b;
                return;
            }

            if (b != 0) return;

            if (CPCSw == 0xc0)
            {
                AYReg = (byte)(AYDat & 0xf);
                CPCSw = 0;
                return;
            }

            if (CPCSw == 0x80)
            {
                if (AYReg < 14) chipRegister.setAY8910Register(0, AYReg, AYDat, model, driver.vgmFrameCounter);
                CPCSw = 0;
            }
        }

        private byte InPort(int address)
        {
            byte ret = 255;
            address = registers.B * 0x100 | (byte)address;
            if ((address & 0xc002) == (0xfffd&0xc002))
            {
                if (AYReg < 14) ret = AYRegMap[AYReg];
            }

            //Console.WriteLine("In Port Adr:{0:x04}", address);
            return ret;
        }

    }
}
