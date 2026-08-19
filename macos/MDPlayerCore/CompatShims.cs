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

    internal static class Resources
    {
        // Mirrors Properties.Resources.cntSettingFileName from the Windows build.
        public const string cntSettingFileName = "Setting.xml";
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
