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
