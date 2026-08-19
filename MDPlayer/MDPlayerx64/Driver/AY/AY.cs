using Konamiman.Z80dotNet;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static MDPlayer.m_hes;
using static System.Net.Mime.MediaTypeNames;

namespace MDPlayer.Driver.AY
{
    public class Information
    {
        public int FileVersion;
        public int PlayerVersion;
        public int PSpecialPlayer;
        public string PAuthor;
        public string PMisc;
        public int NumOfSongs;
        public int FirstSong;
        public List<SongsStructure> SongsStructure;
    }

    public class SongsStructure
    {
        public string PSongName;
        public SongData SongData;
    }

    public class SongData
    {
        public int AChan, BChan, CChan, Noise;
        public int SongLength;
        public int FadeLength;
        public int HiReg, LoReg;
        public Points Points;
        public List<Block> Addresses;
    }

    public class Points
    {
        public int Stack, Init, Inter;
    }

    public class Block
    {
        public int Address;
        public int Length;
        public int Offset;
    }

    public class AY : baseDriver
    {
        private byte[] buf;
        public Information Information;
        private Z80Processor z80;
        public int song = 0;
        private const int ZXclock = 3_546_900;//3.54690MHz
        private const int CPCclock = 4_000_000;//4.000000MHz
        private const double PAL = 50.0;
        private double clkelp = 0.0;
        private double palelp = 0.0;
        private int clock = ZXclock;


        public override bool init(byte[] vgmBuf, ChipRegister chipRegister, EnmModel model, EnmChip[] useChip, uint latency, uint waitTime)
        {
            this.chipRegister = chipRegister;
            LoopCounter = 0;
            vgmCurLoop = 0;
            this.model = model;
            vgmFrameCounter = -latency - waitTime;
            clock = ZXclock;

            try
            {
                Run(vgmBuf);
                Setup(song);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public override bool init(byte[] vgmBuf, int fileType, ChipRegister chipRegister, EnmModel model, EnmChip[] useChip, uint latency, uint waitTime)
        {
            throw new NotImplementedException();
        }

        public override void oneFrameProc()
        {
            try
            {
                vgmSpeedCounter += (double)Common.VGMProcSampleRate / setting.outputDevice.SampleRate * vgmSpeed;
                while (vgmSpeedCounter >= 1.0 && !Stopped)
                {
                    vgmSpeedCounter -= 1.0;
                    if (vgmFrameCounter > -1)
                    {
                        oneFrame();
                        Counter++;
                    }
                    vgmFrameCounter++;
                }
            }
            catch (Exception ex)
            {
                log.ForcedWrite(ex);
            }
        }

        public override GD3 getGD3Info(byte[] buf, uint vgmGd3)
        {
            GetInformation(buf);
            GD3 ret = new GD3();
            ret.TrackName = Information.SongsStructure[0].PSongName;
            ret.TrackNameJ = Information.SongsStructure[0].PSongName;
            return ret;
        }





        public void Run(byte[] buf)
        {
            this.buf = buf;
            //this.mds = mds;
            GetInformation(buf);
        }

        public void SetCPCclock()
        {
            clock = CPCclock;
        }

        public void SetZXclock()
        {
            clock = ZXclock;
        }

        private void GetInformation(byte[] buf)
        {
            //FileID 'ZXAY'
            if (GetUIntLE(buf, 0) != 0x5941_585A) { throw new NotSupportedException(); }
            //TypeID 'EMUL'
            if (GetUIntLE(buf, 4) != 0x4c55_4d45) { throw new NotSupportedException(); }

            Information = new Information();
            Information.FileVersion = buf[8];
            Information.PlayerVersion = buf[9];
            Information.PSpecialPlayer = GetUShortBE(buf, 10);
            Information.PAuthor = GetString(buf, 12);
            Information.PMisc = GetString(buf, 14);
            Information.NumOfSongs = buf[16] + 1;
            Information.FirstSong = buf[17];
            int ptr = GetUShortBE(buf, 18) + 18;
            Information.SongsStructure = new List<SongsStructure>();
            for (int i = 0; i < Information.NumOfSongs; i++)
            {
                SongsStructure ss = new SongsStructure();
                ss.PSongName = GetString(buf, ptr);
                ptr += 2;
                int sptr = GetUShortBE(buf, ptr) + ptr;
                ptr += 2;

                ss.SongData = new SongData();
                ss.SongData.AChan = buf[sptr++];
                ss.SongData.BChan = buf[sptr++];
                ss.SongData.CChan = buf[sptr++];
                ss.SongData.Noise = buf[sptr++];
                ss.SongData.SongLength = GetUShortBE(buf, sptr); sptr += 2;
                ss.SongData.FadeLength = GetUShortBE(buf, sptr); sptr += 2;
                ss.SongData.HiReg = buf[sptr++];
                ss.SongData.LoReg = buf[sptr++];
                int pptr = GetUShortBE(buf, sptr) + sptr; sptr += 2;
                int bptr = GetUShortBE(buf, sptr) + sptr; sptr += 2;

                ss.SongData.Points = new Points();
                ss.SongData.Points.Stack = GetUShortBE(buf, pptr); pptr += 2;
                ss.SongData.Points.Init = GetUShortBE(buf, pptr); pptr += 2;
                ss.SongData.Points.Inter = GetUShortBE(buf, pptr); pptr += 2;

                ss.SongData.Addresses = new List<Block>();
                while (true)
                {
                    Block b = new Block();
                    b.Address = GetUShortBE(buf, bptr); bptr += 2;
                    if (b.Address == 0) break;
                    b.Length = GetUShortBE(buf, bptr); bptr += 2;
                    b.Offset = GetUShortBE(buf, bptr) + bptr; bptr += 2;
                    ss.SongData.Addresses.Add(b);
                }

                Information.SongsStructure.Add(ss);
            }
        }

        private string GetString(byte[] buf, int ptr)
        {
            int ofs = GetUShortBE(buf, ptr) + ptr;
            List<byte> dat = new List<byte>();
            while (buf[ofs] != 0)
            {
                dat.Add(buf[ofs++]);
            }

            return Encoding.ASCII.GetString(dat.ToArray());
        }

        private ushort GetUShortBE(byte[] buf, int ptr)
        {
            return (ushort)(buf[ptr + 1] + (buf[ptr + 0] << 8));
        }

        private ushort GetUShortLE(byte[] buf, int ptr)
        {
            return (ushort)(buf[ptr] + (buf[ptr + 1] << 8));
        }

        private uint GetUIntLE(byte[] buf, int ptr)
        {
            return (uint)(buf[ptr] + (buf[ptr + 1] << 8) + (buf[ptr + 2] << 16) + (buf[ptr + 3] << 24));
        }


        public void Setup(int songNum)
        {
            port port = new port(this);
            z80 = new Z80Processor
            {
                PortsSpace = port,
                ClockFrequencyInMHz = 4,
                ClockSynchronizer = null,
                AutoStopOnRetWithStackEmpty = true,
                AutoStopOnDiPlusHalt = false,
                Memory = new PlainMemory(0x1_0000)
            };
            port.registers = z80.Registers;
            port.chipRegister = chipRegister;
            port.model = model;
            port.CPU = this;

            //a) Fill #0000-#00FF range with #C9 value
            for (int i = 0x0000; i < 0x0100; i++) z80.Memory[i] = 0xc9;
            //b) Fill #0100-#3FFF range with #FF value
            for (int i = 0x0100; i < 0x4000; i++) z80.Memory[i] = 0xff;
            //c) Fill #4000-#FFFF range with #00 value
            for (int i = 0x4000; i < 0x10000; i++) z80.Memory[i] = 0x00;
            //d) Place to #0038 address #FB value
            z80.Memory[0x0038] = 0xfb;

            //e) if INIT equal to ZERO then place to first CALL instruction address of first AY file block instead of INIT(see next f) and g) steps)
            //f) if INTERRUPT equal to ZERO then place at ZERO address next player:
            //g) if INTERRUPT not equal to ZERO then place at ZERO address next player:
            if (songNum >= Information.SongsStructure.Count) songNum = 0;
            int init = Information.SongsStructure[songNum].SongData.Points.Init;
            if (init == 0)
            {
                init = Information.SongsStructure[songNum].SongData.Addresses[0].Address;
            }
            int inter = Information.SongsStructure[songNum].SongData.Points.Inter;
            if (inter == 0)
            {
                byte[] player = new byte[] { 0xF3, 0xCD, 0x00, 0x00, 0xED, 0x5E, 0xFB, 0x76, 0x18, 0xFA };
                for (int i = 0; i < player.Length; i++) z80.Memory[i] = player[i];
            }
            else
            {
                byte[] player = new byte[] { 0xF3, 0xCD, 0x00, 0x00, 0xED, 0x56, 0xFB, 0x76, 0xCD, 0x00, 0x00, 0x18, 0xF7 };
                for (int i = 0; i < player.Length; i++) z80.Memory[i] = player[i];
                z80.Memory[9] = (byte)inter;
                z80.Memory[10] = (byte)(inter >> 8);
            }
            z80.Memory[2] = (byte)init;
            z80.Memory[3] = (byte)(init >> 8);

            //h) Load all blocks for this song
            for (int i = 0; i < Information.SongsStructure[songNum].SongData.Addresses.Count; i++)
            {
                Block block = Information.SongsStructure[songNum].SongData.Addresses[i];
                for (int j = 0; j < block.Length; j++)
                {
                    if (block.Address + j >= 0x1_0000) continue;
                    z80.Memory[block.Address + j] = buf[block.Offset + j];
                }
            }

            //i) Load all common lower registers with LoReg value(including AF register)
            byte lr = (byte)Information.SongsStructure[songNum].SongData.LoReg;
            z80.Registers.F = lr;
            z80.Registers.Alternate.F = lr;
            z80.Registers.L = lr;
            z80.Registers.Alternate.L = lr;
            z80.Registers.E = lr;
            z80.Registers.Alternate.E = lr;
            z80.Registers.C = lr;
            z80.Registers.Alternate.C = lr;
            z80.Registers.IXL = lr;
            z80.Registers.IYL = lr;

            //j) Load all common higher registers with HiReg value
            byte hr = (byte)Information.SongsStructure[songNum].SongData.HiReg;
            z80.Registers.A = hr;
            z80.Registers.Alternate.A = hr;
            z80.Registers.H = hr;
            z80.Registers.Alternate.H = hr;
            z80.Registers.D = hr;
            z80.Registers.Alternate.D = hr;
            z80.Registers.B = hr;
            z80.Registers.Alternate.B = hr;
            z80.Registers.IXH = hr;
            z80.Registers.IYH = hr;

            //k) Load into I register 3(this player version)
            z80.Registers.IR = 0x0300;

            //l) load to SP stack value from points data of this song
            z80.Registers.SP = (short)Information.SongsStructure[songNum].SongData.Points.Stack;

            //m) Load to PC ZERO value
            z80.Registers.PC = 0;

            //n) Disable Z80 interrupts and set IM0 mode
            z80.InterruptMode = 0;

            //o) Emulate resetting of AY chip
            //p) Start Z80 emulation
        }

        public void oneFrame()
        {
            double old = z80.TStatesElapsedSinceReset;
            bool brk = false;

            while (!brk)
            {
                z80.ExecuteNextInstruction();
                double step = z80.TStatesElapsedSinceReset - old;
                old = z80.TStatesElapsedSinceReset;

                clkelp += step;
                if (clock / setting.outputDevice.SampleRate <= clkelp)
                {
                    clkelp -= (clock / setting.outputDevice.SampleRate);
                    brk = true;
                }

                palelp += step;
                if (clock / PAL <= palelp)
                {
                    palelp -= (clock / PAL);

                    if (z80.IsHalted)
                    {
                        int pc = z80.Registers.PC;
                        int sp = z80.Registers.SP;
                        int af = z80.Registers.AF;
                        z80.Reset();
                        z80.Registers.PC = (ushort)pc;
                        z80.Registers.SP = (short)sp;
                        z80.Registers.AF = (short)af;
                        brk = true;//HALTの時のみループから抜ける。(Z80は次の処理のタイミングまで休む必要があるため)
                    }

                    //ここでbrk=trueしてもほとんど問題ないがフレーム落ちを再現できなくなる

                }
            }

            //if (ps && z80.IsHalted)
            //{
            //    int pc = z80.Registers.PC;
            //    int sp = z80.Registers.SP;
            //    int af = z80.Registers.AF;
            //    z80.Reset();
            //    z80.Registers.PC = (ushort)pc;
            //    z80.Registers.SP = (short)sp;
            //    z80.Registers.AF = (short)af;
            //    //Console.WriteLine("PC:{0:x04}", z80.Registers.PC);
            //}

        }

    }

}
