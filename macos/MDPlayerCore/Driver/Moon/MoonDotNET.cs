using musicDriverInterface;

namespace MDPlayer.Driver.Moon
{
    // Cross-platform adapter for MoonDriverDotNET's MDR/MDL sequence engine.
    public sealed class MoonDotNET : baseDriver
    {
        private MoonDriverDotNET.Driver.Driver driver;
        public string SourcePath { get; set; }
        public bool UseOpl3 { get; set; }

        public override GD3 getGD3Info(byte[] data, uint vgmGd3) => new();

        public override bool init(byte[] data, ChipRegister register, EnmModel playbackModel,
            EnmChip[] requestedChips, uint requestedLatency, uint requestedWaitTime)
        {
            if (data == null || data.Length < 8) return false;
            vgmBuf = data; chipRegister = register; model = playbackModel; useChip = requestedChips;
            latency = requestedLatency; waitTime = requestedWaitTime;
            Counter = TotalCounter = LoopCounter = 0; vgmCurLoop = 0;
            vgmFrameCounter = -requestedLatency - requestedWaitTime; vgmSpeed = 1; vgmSpeedCounter = 0; Stopped = false;
            try
            {
                driver = new MoonDriverDotNET.Driver.Driver();
                driver.Init(SourcePath ?? "music.mdr", data, WriteOpl, Common.VGMProcSampleRate,
                    new MoonDriverDotNET.Driver.MoonDriverDotNETOption(), Array.Empty<string>(), OpenCompanionFile);
                return true;
            }
            catch { Stopped = true; return false; }
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
                    vgmSpeedCounter -= 1.0; driver.Rendering(); Counter++; vgmFrameCounter++;
                }
            }
            catch { Stopped = true; }
        }
        public static byte[] CompileMdl(byte[] source, string sourcePath, out string error)
        {
            error = string.Empty;
            try
            {
                MoonDriverDotNET.Compiler.Compiler compiler = new();
                compiler.Init(); compiler.isSrc = true; compiler.origpath = sourcePath;
                using MemoryStream input = new(source, writable: false);
                MmlDatum[] output = compiler.Compile(input, name => OpenCompanionFile(sourcePath, name));
                var info = compiler.GetCompilerInfo();
                if (output == null || info?.errorList?.Count > 0)
                {
                    error = info?.errorList?.Count > 0
                        ? string.Join(System.Environment.NewLine, info.errorList.Select(item => item.Item3))
                        : "MoonDriver MDL compilation failed.";
                    return null;
                }
                return output.Select(item => item == null ? (byte)0 : (byte)item.dat).ToArray();
            }
            catch (Exception exception) { error = exception.Message; return null; }
        }
        private void WriteOpl(ChipDatum value)
        {
            if (value == null || value.port < 0 || value.address < 0 || value.data < 0) return;
            if (UseOpl3) chipRegister.setYMF262Register(0, value.port, value.address, value.data, model);
            else chipRegister.setYMF278BRegister(0, value.port, value.address, value.data, model);
        }
        private Stream OpenCompanionFile(string path)
            => OpenCompanionFile(SourcePath, path) ?? new MemoryStream();
        private static Stream OpenCompanionFile(string sourcePath, string requestedName)
        {
            try
            {
                string root = Path.GetFullPath(Path.GetDirectoryName(sourcePath));
                string candidate = Path.IsPathRooted(requestedName)
                    ? Path.GetFullPath(requestedName)
                    : Path.GetFullPath(Path.Combine(root, requestedName.Replace('\\', Path.DirectorySeparatorChar)));
                string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
                return candidate.StartsWith(prefix, StringComparison.Ordinal) && File.Exists(candidate) ? File.OpenRead(candidate) : null;
            }
            catch { return null; }
        }
    }
}
