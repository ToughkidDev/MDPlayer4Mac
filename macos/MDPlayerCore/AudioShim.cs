// TODO(macOS port): `Audio` (originally Audio.cs, ~13,600 lines) is MDPlayer's real playback
// orchestration engine — NAudio device output (WASAPI/ASIO), MIDI device enumeration,
// Ogg/Vorbis/FLAC decoding, and file-format detection across every supported driver family
// (VGM, MGS, MuSICA, NDP, FMP, PMDDotNET, MoonDriverDotNET, muapDotNET, NRTDRV, MucomDotNET —
// most of which are not yet vendored into MDPlayerCore at all). It is genuinely out of scope
// for the "portable engine core" milestone: real audio output needs a CoreAudio-backed
// replacement (see macos/README.md's "아직 손 안 댄 것" section), and file-format detection
// pulls in driver families this port hasn't reached yet.
//
// A handful of call sites elsewhere in the ported tree (ChipRegister.cs, PlayList.cs,
// PianoRoll/*.cs) reference a few `Audio.xxx` static members as cross-cutting "what's
// currently playing" state rather than as part of the audio-output pipeline itself. This
// file provides just that surface so those files compile. GetMusic (file-format detection/
// metadata extraction) is real, substantial, portable logic in the original — but pulling it
// in means vendoring several driver families this port hasn't touched yet, so for now it
// throws. Revisit when building the VGM-to-WAV smoke test app (macos/README.md's next-step
// candidate): either vendor the real GetMusic + its driver dependencies, or narrow PlayList's
// needs down to just the drivers already ported (VGM/XGM/SID/MNDRV/ZMS/MXDRV).
namespace MDPlayer
{
    public static class Audio
    {
        public static int ClockAY8910 { get; set; } = 1789750;
        public static int ClockK051649 { get; set; } = 1500000;
        public static int ClockSN76489 { get; set; } = 0;
        public static int ClockYM2608 { get; set; } = 0;
        public static int ClockYM2610 { get; set; } = 0;
        public static int ClockYM2612 { get; set; } = 0;

        public static baseDriver DriverVirtual { get; set; } = null;
        public static EnmFileFormat PlayingFileFormat;

        public static System.Collections.Generic.List<PlayList.Music> GetMusic(string file, byte[] buf, string zipFile = null, object entry = null)
        {
            throw new System.NotImplementedException(
                "Audio.GetMusic (file-format detection/metadata) is not yet ported to macOS — see AudioShim.cs.");
        }
    }
}
