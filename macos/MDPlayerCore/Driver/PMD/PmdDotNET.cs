using musicDriverInterface;
using PMDDotNET.Common;
using PMDDotNET.Driver;

namespace MDPlayer.Driver.PMD
{
    // Adapter for the official PMDDotNET sequence driver.  It deliberately keeps the
    // compiler and PCM-file resolution in-process, like the Windows implementation, so a
    // selected .mml never needs a temporary .M file beside the user's source.
    public sealed class PmdDotNET : baseDriver
    {
        public const uint OPNABaseClock = 7_987_200;
        private PMDDotNET.Driver.Driver driver;
        public string SourcePath { get; set; }

        // PMD metadata lives in the compiled stream and is optional.  Playback must not
        // depend on decoding it, so retain the base player's empty-but-valid fallback.
        public override GD3 getGD3Info(byte[] data, uint vgmGd3) => new();

        public override bool init(byte[] data, ChipRegister register, EnmModel playbackModel,
            EnmChip[] requestedChips, uint requestedLatency, uint requestedWaitTime)
        {
            if (data == null || data.Length == 0) return false;
            vgmBuf = data;
            chipRegister = register;
            model = playbackModel;
            useChip = requestedChips;
            latency = requestedLatency;
            waitTime = requestedWaitTime;
            Counter = TotalCounter = LoopCounter = 0;
            vgmCurLoop = 0;
            vgmFrameCounter = -requestedLatency - requestedWaitTime;
            vgmSpeed = 1;
            vgmSpeedCounter = 0;
            Stopped = false;

            try
            {
                driver = new PMDDotNET.Driver.Driver();
                PMDDotNETOption options = new()
                {
                    isAUTO = true,
                    isNRM = true,
                    usePPS = true,
                    usePPZ = true,
                    srcFile = SourcePath ?? string.Empty,
                    jumpIndex = -1,
                };
                driver.Init(data, WriteOpna, (_, _) => { }, options, Array.Empty<string>(),
                    OpenSiblingFile, WritePpz8, WritePpsDrv, WriteP86);
                driver.StartRendering(Common.VGMProcSampleRate,
                    new[] { Tuple.Create("YM2608", (int)OPNABaseClock) });
                driver.MusicSTART(0);
                return true;
            }
            catch
            {
                Stopped = true;
                return false;
            }
        }

        public override bool init(byte[] data, int fileType, ChipRegister register, EnmModel playbackModel,
            EnmChip[] requestedChips, uint requestedLatency, uint requestedWaitTime)
            => init(data, register, playbackModel, requestedChips, requestedLatency, requestedWaitTime);

        public override void oneFrameProc()
        {
            if (Stopped || driver == null) return;
            try
            {
                vgmSpeedCounter += (double)Common.VGMProcSampleRate / setting.outputDevice.SampleRate * vgmSpeed;
                while (vgmSpeedCounter >= 1.0)
                {
                    vgmSpeedCounter -= 1.0;
                    driver.Rendering();
                    Counter++;
                    vgmFrameCounter++;
                }
                vgmCurLoop = (uint)Math.Max(0, driver.GetNowLoopCounter());
                if (driver.GetStatus() < 1) Stopped = true;
            }
            catch { Stopped = true; }
        }

        public static byte[] CompileMml(byte[] source, string sourcePath, out string error)
        {
            error = string.Empty;
            try
            {
                PMDDotNET.Compiler.Compiler compiler = new();
                compiler.Init();
                compiler.env = new PMDDotNET.Common.Environment().GetEnv();
                using MemoryStream input = new(source, writable: false);
                MmlDatum[] output = compiler.Compile(input, name => OpenSiblingFile(sourcePath, name));
                CompilerInfo info = compiler.GetCompilerInfo();
                if (output == null || info?.errorList?.Count > 0)
                {
                    error = info?.errorList?.Count > 0
                        ? string.Join(System.Environment.NewLine, info.errorList.Select(item => item.Item3))
                        : "PMD MML compilation failed.";
                    return null;
                }
                return output.Select(item => item == null ? (byte)0 : (byte)item.dat).ToArray();
            }
            catch (Exception exception) { error = exception.Message; return null; }
        }

        private void WriteOpna(ChipDatum value)
        {
            if (value != null && value.port >= 0 && value.address >= 0 && value.data >= 0)
                chipRegister.setYM2608Register(0, value.port, value.address, value.data, model, vgmFrameCounter);
        }
        private int WritePpz8(ChipDatum value)
        {
            if (value != null) chipRegister.PPZ8Write(0, value.port, value.address, value.data, model);
            return 0;
        }
        private int WritePpsDrv(ChipDatum value)
        {
            if (value != null) chipRegister.PPSDRVWrite(0, value.port, value.address, value.data, model);
            return 0;
        }
        private int WriteP86(ChipDatum value)
        {
            if (value != null) chipRegister.P86Write(0, value.port, value.address, value.data, model);
            return 0;
        }
        private Stream OpenSiblingFile(string name) => OpenSiblingFile(SourcePath, name);
        private static Stream OpenSiblingFile(string sourcePath, string requestedName)
        {
            try
            {
                string directory = Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(requestedName)) return null;
                string root = Path.GetFullPath(directory);
                string candidate = Path.GetFullPath(Path.Combine(root, requestedName.Replace('\\', Path.DirectorySeparatorChar)));
                string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
                return candidate.StartsWith(prefix, StringComparison.Ordinal) && File.Exists(candidate)
                    ? File.OpenRead(candidate) : null;
            }
            catch { return null; }
        }
    }
}
