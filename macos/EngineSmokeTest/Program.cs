// Minimal, purpose-built VGM -> WAV renderer for verifying the ported MDPlayerCore engine
// actually plays audio, not just compiles. Deliberately NOT a port of MDPlayerx64/Audio.cs's
// VgmPlay/TrdVgmVirtualFunction (that method wires up ~20 possible VGM chip types plus
// fadeout/hiyorimi/MIDI-passthrough/real-hardware bookkeeping meant for a live, interactive
// player - see macos/MDPlayerCore/AudioShim.cs's header comment for why that's out of scope
// for now). The actual "wire a VGM buffer up to Vgm+ChipRegister+MDSound" setup lives in
// macos/MDPlayerCore/VgmEngine.cs (shared with macos/LivePlayer/'s real-time playback), so
// this file is just the WAV-writing loop around it.
using MDPlayer;

namespace MDPlayer.EngineSmokeTest
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: EngineSmokeTest <input.vgm> [output.wav]");
                return 1;
            }

            string vgmPath = args[0];
            if (!File.Exists(vgmPath))
            {
                Console.Error.WriteLine($"error: file not found: {vgmPath}");
                return 1;
            }

            string outDir = args.Length >= 2
                ? Path.GetDirectoryName(Path.GetFullPath(args[1]))
                : Path.GetDirectoryName(Path.GetFullPath(vgmPath));
            string outName = args.Length >= 2
                ? Path.GetFileName(args[1])
                : Path.GetFileNameWithoutExtension(vgmPath) + ".wav";
            if (string.IsNullOrEmpty(outDir)) outDir = ".";

            byte[] vgmBuf = File.ReadAllBytes(vgmPath);
            Console.WriteLine($"Loaded {vgmPath} ({vgmBuf.Length} bytes)");

            VgmEngineSession session = VgmEngine.Load(vgmBuf);
            if (session == null)
            {
                Console.Error.WriteLine("error: Vgm.init() failed, or the file uses none of the chips VgmEngine.Load wires up (see VgmEngine.cs)");
                return 1;
            }

            Vgm vgm = session.Vgm;
            MDSound.MDSound mds = session.Mds;
            Setting setting = session.Setting;
            uint sampleRate = session.SampleRate;

            Console.WriteLine($"VGM version {vgm.Version}, chips: {session.DescribeActiveChips()}");

            setting.other.WavSwitch = true;
            setting.other.WavPath = outDir;

            // WaveWriter.Open() derives its own output filename from whatever path it's given
            // (Path.GetFileNameWithoutExtension(filename) + ".wav", under setting.other.WavPath) -
            // it's designed to be handed the source music file's path, not a pre-built .wav path.
            // Pass vgmPath (not a .wav-suffixed path) so its collision guard (which appends "_0"
            // if the derived name would equal the path it was given) doesn't fire spuriously.
            WaveWriter waveWriter = new(setting);
            waveWriter.Open(vgmPath);
            string derivedWavPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(vgmPath) + ".wav");

            const int chunkSamples = 4096; // stereo interleaved shorts, must be even
            short[] buffer = new short[chunkSamples];
            long totalSamplesWritten = 0;
            int safetyLimitChunks = 60 * (int)(sampleRate * 2 / chunkSamples) + 1000; // ~60s hard cap so a driver bug can't hang forever

            for (int chunk = 0; chunk < safetyLimitChunks && !vgm.Stopped; chunk++)
            {
                int written = mds.Update(buffer, 0, chunkSamples, vgm.oneFrameProc);
                if (written <= 0) break;
                waveWriter.Write(buffer, 0, written);
                totalSamplesWritten += written / 2;
            }

            waveWriter.Close();

            // Optional rename to the caller-requested output name (args[1]), since WaveWriter
            // itself always derives the name from the input file's stem.
            string finalPath = derivedWavPath;
            if (args.Length >= 2)
            {
                string requestedPath = Path.Combine(outDir, outName);
                string fullRequested = Path.GetFullPath(requestedPath);
                string fullDerived = Path.GetFullPath(derivedWavPath);
                if (!string.Equals(fullRequested, fullDerived, StringComparison.Ordinal) && File.Exists(derivedWavPath))
                {
                    File.Move(derivedWavPath, requestedPath, overwrite: true);
                    finalPath = requestedPath;
                }
            }

            Console.WriteLine($"Wrote {finalPath} ({totalSamplesWritten} stereo samples, {totalSamplesWritten / (double)sampleRate:F2}s @ {sampleRate}Hz)");

            return 0;
        }
    }
}
