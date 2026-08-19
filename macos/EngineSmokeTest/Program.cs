// Minimal, purpose-built VGM -> WAV renderer for verifying the ported MDPlayerCore engine
// actually plays audio, not just compiles. Deliberately NOT a port of MDPlayerx64/Audio.cs's
// VgmPlay/TrdVgmVirtualFunction (that method wires up ~20 possible VGM chip types plus
// fadeout/hiyorimi/MIDI-passthrough/real-hardware bookkeeping meant for a live, interactive
// player - see macos/MDPlayerCore/AudioShim.cs's header comment for why that's out of scope
// for now). This program only wires up the two chips a Sega Mega Drive VGM actually needs
// (SN76489 PSG + YM2612 FM), matching the project's own namesake, and drives the engine
// synchronously to a file instead of a live device callback.
//
// Pipeline, modeled on Audio.VgmPlay/TrdVgmVirtualMainFunction (MDPlayer/MDPlayerx64/Audio.cs):
//   1. Setting.Load() + Setting.ApplyChipTypeDefaults() (macos/MDPlayerCore/Setting.cs) -
//      populate the per-chip ChipType2 defaults ChipRegister's constructor requires.
//   2. ChipRegister(setting, ..., mds, ...) - all real-hardware arguments (RealChip/RSoundChip[])
//      passed as null; this build's RealChip is a no-op stub anyway (see CompatShims.cs).
//   3. new Vgm(setting) -> Vgm.init(vgmBytes, chipRegister, EnmModel.VirtualModel, ...) - parses
//      the VGM header (chip clocks) and readies the command stream.
//   4. Build the MDSound.MDSound.Chip[] list for whichever of SN76489/YM2612 the file declares
//      a nonzero clock for, then chipRegister.initChipRegister(chips) + mds.Init(..., chips).
//   5. Loop mds.Update(buffer, 0, chunkSamples, vgm.oneFrameProc) - same call shape as
//      TrdVgmVirtualMainFunction's `cnt = mds.Update(buffer, offset, sampleCount, oneFrameProc);` -
//      until vgm.Stopped, writing each chunk to WaveWriter.
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

            Setting setting = Setting.Load();
            setting.ApplyChipTypeDefaults();
            setting.other.WavSwitch = true;
            setting.other.WavPath = outDir;

            uint sampleRate = (uint)setting.outputDevice.SampleRate;
            uint samplingBuffer = 2048;

            MDSound.MDSound mds = new(sampleRate, samplingBuffer, null);

            ChipRegister chipRegister = new(
                setting
                , null // pianoRollMng - not exercised by this smoke test
                , mds
                , null // RealChip nScci - out of scope (see CompatShims.cs), always a no-op
                , new RSoundChip[] { null, null } // scYM2612
                , new RSoundChip[] { null, null } // scSN76489
                , new RSoundChip[] { null, null } // scYM2608
                , new RSoundChip[] { null, null } // scYM2151
                , new RSoundChip[] { null, null } // scYM2151_4M
                , new RSoundChip[] { null, null } // scYM2203
                , new RSoundChip[] { null, null } // scYM2413
                , new RSoundChip[] { null, null } // scYM2610
                , new RSoundChip[] { null, null } // scYM2610EA
                , new RSoundChip[] { null, null } // scYM2610EB
                , new RSoundChip[] { null, null } // scYM3526
                , new RSoundChip[] { null, null } // scYM3812
                , new RSoundChip[] { null, null } // scYMF262
                , new RSoundChip[] { null, null } // scC140
                , new RSoundChip[] { null, null } // scSEGAPCM
                , new RSoundChip[] { null, null } // scAY8910
                , new RSoundChip[] { null, null } // scK051649
            );

            Vgm vgm = new(setting);
            vgm.dacControl.chipRegister = chipRegister;
            vgm.dacControl.model = EnmModel.VirtualModel;

            uint latency = 0;
            uint waitTime = 0;
            if (!vgm.init(vgmBuf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.SN76489, EnmChip.YM2612 }, latency, waitTime))
            {
                Console.Error.WriteLine("error: Vgm.init() failed (not a valid VGM file?)");
                return 1;
            }

            Console.WriteLine($"VGM version {vgm.Version}, chips: SN76489={vgm.SN76489ClockValue}Hz YM2612={vgm.YM2612ClockValue}Hz");

            List<MDSound.MDSound.Chip> lstChips = new();

            if (vgm.SN76489ClockValue != 0)
            {
                MDSound.sn76489 sn76489 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.SN76489,
                    ID = 0,
                    Instrument = sn76489,
                    Update = sn76489.Update,
                    Start = sn76489.Start,
                    Stop = sn76489.Stop,
                    Reset = sn76489.Reset,
                    SamplingRate = sampleRate,
                    Volume = 0,
                    Clock = vgm.SN76489ClockValue,
                    Option = null,
                });
            }

            if (vgm.YM2612ClockValue != 0)
            {
                MDSound.ym2612 ym2612 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2612,
                    ID = 0,
                    Instrument = ym2612,
                    Update = ym2612.Update,
                    Start = ym2612.Start,
                    Stop = ym2612.Stop,
                    Reset = ym2612.Reset,
                    SamplingRate = sampleRate,
                    Volume = 0,
                    Clock = vgm.YM2612ClockValue,
                    Option = null,
                });
            }

            if (lstChips.Count == 0)
            {
                Console.Error.WriteLine("error: this smoke test only wires up SN76489/YM2612, and the VGM file uses neither");
                return 1;
            }

            chipRegister.initChipRegister(lstChips.ToArray());
            mds.Init(sampleRate, samplingBuffer, lstChips.ToArray());

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
