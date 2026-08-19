using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayer.Driver.GBS
{
    public class Registers
    {
        public Registers() { }
        public byte a, b, c, d, e, f, h, l;
        public ushort sp, pc;

        public ushort af
        {
            get
            {
                return (ushort)((a << 8) | f);
            }
            set
            {
                a = (byte)(value >> 8);
                f = (byte)value;
            }
        }

        public ushort bc
        {
            get
            {
                return (ushort)((b << 8) | c);
            }
            set
            {
                b = (byte)(value >> 8);
                c = (byte)value;
            }
        }

        public ushort de
        {
            get
            {
                return (ushort)((d << 8) | e);
            }
            set
            {
                d = (byte)(value >> 8);
                e = (byte)value;
            }
        }

        public ushort hl
        {
            get
            {
                return (ushort)((h << 8) | l);
            }
            set
            {
                h = (byte)(value >> 8);
                l = (byte)value;
            }
        }

        public bool Z
        {
            get
            {
                return (f & 0x80) != 0;
            }
            set
            {
                f &= 0x7f;
                f |= (byte)(value ? 0x80 : 0);
            }
        }

        public bool S
        {
            get
            {
                return (f & 0x40) != 0;
            }
            set
            {
                f &= 0xbf;
                f |= (byte)(value ? 0x40 : 0);
            }
        }

        public bool H
        {
            get
            {
                return (f & 0x20) != 0;
            }
            set
            {
                f &= 0xdf;
                f |= (byte)(value ? 0x20 : 0);
            }
        }

        public bool C
        {
            get
            {
                return (f & 0x10) != 0;
            }
            set
            {
                f &= 0xef;
                f |= (byte)(value ? 0x10 : 0);
            }
        }

        public override string ToString()
        {
            //string n = string.Format(@"A:${0:x02} B:${1:x02} C:${2:x02} D:${3:x02} E:${4:x02} F:${5:x02} H:${6:x02} L:${7:x02} AF:${0:x02}{5:x02} BC:${1:x02}{2:x02} DE:${3:x02}{4:x02} HL:${6:x02}{7:x02} SP:${8:x04} PC:${9:x04} FLAG:{10}{11}{12}{13}",
            //    a, b, c, d, e, f, h, l, sp, pc, Z ? "Z" : "-", S ? "S" : "-", H ? "H" : "-", C ? "C" : "-");
            string n = string.Format(@"A{0:x02} B{1:x02} C{2:x02} D{3:x02} E{4:x02} F{5:x02} H{6:x02} L{7:x02} SP{8:x04} PC{9:x04} Flg{10}{11}{12}{13}",
                a, b, c, d, e, f, h, l, sp, pc, Z ? "Z" : "-", S ? "S" : "-", H ? "H" : "-", C ? "C" : "-");
            return n;
        }

    }
}
