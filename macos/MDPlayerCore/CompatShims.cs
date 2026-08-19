// Minimal cross-platform shims for a handful of System.Drawing / System.Windows.Forms
// types referenced by the ported engine code (Setting.cs). The original Windows build
// gets these implicitly from the WindowsDesktop SDK (UseWindowsForms=true); this portable
// build defines just the tiny surface actually used, so we avoid pulling in
// System.Drawing.Common (and its Windows-only GDI+ baggage) for what amounts to a
// couple of plain value structs.
namespace MDPlayer
{
    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Point Empty => new Point(0, 0);
    }

    public struct Size
    {
        public int Width;
        public int Height;

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public static Size Empty => new Size(0, 0);
    }

    public enum FormWindowState
    {
        Normal,
        Minimized,
        Maximized,
    }

    // TODO(macOS port): PlayList.cs builds its rows directly against a bound WinForms
    // DataGridView (by column name -> index lookup, CreateCells, Cells[i].Value/.ToolTipText,
    // Rows.Add) instead of keeping a plain data model — the original app has no separation
    // between "playlist data" and "playlist grid presentation". Rather than refactor
    // PlayList.cs itself (which would ripple into every caller, and we don't know yet what
    // shape the Avalonia UI will want), this is a compile-time-only shim reproducing just the
    // subset of DataGridView's API PlayList.cs touches. Nothing constructs/wires a real grid
    // here yet, so calling these at runtime today gets you an empty, unpopulated grid, not a
    // crash — but also not a populated playlist. Revisit when the UI layer needs playlist
    // display: either give this shim real backing storage, or refactor PlayList.cs to expose
    // a plain list and let the UI layer build rows itself.
    public class DataGridViewCell
    {
        public object Value { get; set; }
        public string ToolTipText { get; set; }
    }

    public class DataGridViewCellCollection : System.Collections.Generic.List<DataGridViewCell>
    {
    }

    public class DataGridViewRow
    {
        public DataGridViewCellCollection Cells { get; } = new();
        public object Tag { get; set; }

        public void CreateCells(DataGridView dgv)
        {
            Cells.Clear();
            for (int i = 0; i < dgv.Columns.Count; i++)
            {
                Cells.Add(new DataGridViewCell());
            }
        }
    }

    public class DataGridViewRowCollection : System.Collections.Generic.List<DataGridViewRow>
    {
    }

    public class DataGridViewColumn
    {
        public string Name { get; set; }
        public int Index { get; set; }
    }

    public class DataGridViewColumnCollection
    {
        private readonly System.Collections.Generic.List<DataGridViewColumn> columns = new();

        public int Count => columns.Count;

        public DataGridViewColumn this[string name]
        {
            get
            {
                foreach (DataGridViewColumn c in columns)
                {
                    if (c.Name == name) return c;
                }
                throw new System.Collections.Generic.KeyNotFoundException(name);
            }
        }

        public void Add(DataGridViewColumn column)
        {
            column.Index = columns.Count;
            columns.Add(column);
        }
    }

    public class DataGridView
    {
        public DataGridViewColumnCollection Columns { get; } = new();
        public DataGridViewRowCollection Rows { get; } = new();
    }

    internal static class Resources
    {
        // Mirrors the handful of Properties.Resources string entries from the Windows
        // build's Resources.resx that the ported code actually reads (log.cs, Setting.cs).
        public const string cntSettingFileName = "Setting.xml";
        public const string cntLogFilename = "log.txt";
        public const string cntTimeFormat = "yyMMddHHmmssfff";
        public const string cntExceptionFormat = "例外発生:\r\n- Type ------\r\n{0}\r\n- Message ------\r\n{1}\r\n- Source ------\r\n{2}\r\n- StackTrace ------\r\n{3}\r\n";
        public const string cntInnerExceptionFormat = "内部例外:\r\n- Type ------\r\n{0}\r\n- Message ------\r\n{1}\r\n- Source ------\r\n{2}\r\n- StackTrace ------\r\n{3}\r\n";
    }

    // TODO(macOS port): real physical sound-chip hardware support (SCCI / C86Ctrl / NiseC86Ctrl
    // boards — see the original RealChip.cs) is stubbed out. Those interfaces are Windows-only
    // driver libraries (NScci, Nc86ctl, NiseC86ctl — not vendored here) for people who own actual
    // FM synth chip add-in hardware; out of scope for a software-emulation-only macOS build.
    // RSoundChip itself (below) is copied verbatim from RealChip.cs — it's the plain abstract
    // base class ChipRegister programs against and has no external dependency; only the concrete
    // Windows-hardware subclasses (RScciSoundChip etc.) were left out.
    public class RSoundChip
    {
        protected int SoundLocation;
        protected int BusID;
        protected int SoundChip;

        public uint dClock = 3579545;

        public RSoundChip(int soundLocation, int busID, int soundChip)
        {
            SoundLocation = soundLocation;
            BusID = busID;
            SoundChip = soundChip;
        }

        virtual public void Init()
        {
            throw new NotImplementedException();
        }

        virtual public void SetRegister(int adr, int dat)
        {
            throw new NotImplementedException();
        }

        virtual public int GetRegister(int adr)
        {
            throw new NotImplementedException();
        }

        virtual public bool IsBufferEmpty()
        {
            throw new NotImplementedException();
        }

        virtual public uint SetMasterClock(uint mClock)
        {
            throw new NotImplementedException();
        }

        virtual public void SetSSGVolume(byte vol)
        {
            throw new NotImplementedException();
        }
    }

    public class RealChip : IDisposable
    {
        public RealChip(bool sw)
        {
        }

        public void SendData()
        {
            // no-op: nothing to flush to real hardware in the macOS build.
        }

        public void Dispose()
        {
        }
    }

    // TODO(macOS port): ChipRegister.cs also directly type-checks/casts to the concrete
    // C86Ctrl-backed subclass (RC86ctlSoundChip) and reads its ChipType, beyond just the
    // abstract RSoundChip base above (clock-doubling logic for OPNA/OPN3L/YM2149/OPL3 real
    // hardware). Since RealChip itself is a no-op stub, nothing in this build actually
    // constructs an RC86ctlSoundChip, but the type still needs to exist and be a
    // RSoundChip for ChipRegister.cs to compile. Bare-minimum stub; extend with real
    // Nc86ctl.ChipType members if/when real hardware support returns.
    public class RC86ctlSoundChip : RSoundChip
    {
        public RC86ctlSoundChip(int soundLocation, int busID, int soundChip)
            : base(soundLocation, busID, soundChip)
        {
        }

        public Nc86ctl.ChipType ChipType { get; set; }
    }

    // TODO(macOS port): WinForms Application shim — only the tiny subset the ported code
    // actually reads (ExecutablePath, used to locate the app's own directory for settings/
    // plugin files) is provided. No real Application/message-loop semantics here.
    public static class Application
    {
        public static string ExecutablePath { get; set; } =
            System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    // TODO(macOS port): WinForms MessageBox shim. There is no UI layer yet in this portable
    // build, so .Show() just logs to the console instead of popping a dialog. Revisit once
    // the Avalonia UI layer exists — likely routed through a real dialog service instead.
    public enum MessageBoxButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel,
    }

    public enum MessageBoxIcon
    {
        None,
        Error,
        Warning,
        Information,
        Question,
    }

    public static class MessageBox
    {
        public static void Show(string text)
        {
            System.Console.WriteLine("[MessageBox] " + text);
        }

        public static void Show(string text, string caption)
        {
            System.Console.WriteLine($"[MessageBox] {caption}: {text}");
        }

        public static void Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            System.Console.WriteLine($"[MessageBox:{icon}] {caption}: {text}");
        }
    }
}

// TODO(macOS port): out-of-scope real-hardware chip-type enum (see RC86ctlSoundChip above).
// Only the members ChipRegister.cs actually compares against are included.
namespace Nc86ctl
{
    public enum ChipType
    {
        CHIP_OPNA,
        CHIP_OPN3L,
        CHIP_YM2149,
        CHIP_OPL3,
    }
}

// TODO(macOS port): UnlhaWrap wraps the Windows-only unlha32.dll (LHA archive extraction)
// via LoadLibrary/GetProcAddress P/Invoke — genuinely not portable. Stubbed with the two
// members PlayList.cs calls (LHA-archived playlist entries); throws until a real
// cross-platform LHA extractor is wired in (shell out to `lha`/`7z`, or a managed decoder).
namespace UnlhaWrap
{
    public class UnlhaCmd
    {
        public System.Collections.Generic.List<System.Tuple<string, ulong>> GetFileList(string archiveFile, string wildCard)
        {
            throw new NotImplementedException("LHA archive support is not yet ported to macOS.");
        }

        public byte[] GetFileByte(string archiveFile, string fileName)
        {
            throw new NotImplementedException("LHA archive support is not yet ported to macOS.");
        }
    }
}

namespace NAudio.Midi
{
    // TODO(macOS port): real external-hardware MIDI-out passthrough (ChipRegister.setMIDIout
    // and friends) is stubbed out for now — it drove real MIDI interfaces on Windows via
    // winmm.dll through NAudio. A working macOS build needs a CoreMIDI-backed replacement
    // (e.g. via a cross-platform MIDI package) wired in here before that feature works;
    // until then these calls are no-ops so the engine compiles and runs without it.
    public class MidiOut
    {
        public void SendBuffer(byte[] data) { }
        public void Reset() { }
        public void Close() { }
    }
}
