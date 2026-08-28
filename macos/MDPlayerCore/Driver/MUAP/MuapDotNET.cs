using musicDriverInterface;

namespace MDPlayer.Driver.MUAP
{
    public sealed class MuapDotNET : baseDriver
    {
        private muapDotNET.Driver.Driver driver;
        public string SourcePath { get; set; }
        public byte[] ToneBuffer { get; set; }
        public ushort[] LabelAddresses { get; set; }
        public override GD3 getGD3Info(byte[] data, uint vgmGd3) => new();

        public override bool init(byte[] data, ChipRegister register, EnmModel playbackModel,
            EnmChip[] requestedChips, uint requestedLatency, uint requestedWaitTime)
        {
            if (data == null || data.Length == 0) return false;
            vgmBuf = data; chipRegister = register; model = playbackModel; useChip = requestedChips;
            latency = requestedLatency; waitTime = requestedWaitTime; Counter = TotalCounter = LoopCounter = 0;
            vgmCurLoop = 0; vgmFrameCounter = -requestedLatency - requestedWaitTime; vgmSpeed = 1; vgmSpeedCounter = 0; Stopped = false;
            try
            {
                driver = new muapDotNET.Driver.Driver(System.Environment.GetEnvironmentVariables());
                List<ChipAction> actions = new() { new ActionSink(WriteOpna), new ActionSink(WriteOpn2), new ActionSink(WriteCs4231) };
                MmlDatum[] input = data.Select(value => new MmlDatum(value)).ToArray();
                driver.Init(actions, input, null, new object[]
                {
                    (Func<byte, byte>)ReadCs4231,
                    (Func<byte[]>)GetCs4231Map,
                    (iDriver.dlgEMS_Map)MapCs4231,
                    (Func<ushort>)GetCs4231PageMap,
                    (iDriver.dlgEMS_GetHandleName)GetCs4231HandleName,
                    (iDriver.dlgEMS_SetHandleName)SetCs4231HandleName,
                    (iDriver.dlgEMS_AllocMemory)AllocateCs4231Memory,
                    ToneBuffer, 0, LabelAddresses, Path.GetDirectoryName(SourcePath) ?? string.Empty,
                });
                driver.StartRendering(Common.VGMProcSampleRate, new[] { Tuple.Create("YM2608", 7_987_200) });
                driver.MusicSTART(0);
                object[] work = (object[])driver.GetWork();
                if (work?.Length > 0 && work[0] is byte[] fifo) chipRegister.setCS4231FIFOBuf(0, fifo, model);
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
                while (vgmSpeedCounter >= 1.0) { vgmSpeedCounter -= 1.0; driver.Rendering(); Counter++; vgmFrameCounter++; }
                vgmCurLoop = (uint)Math.Max(0, driver.GetNowLoopCounter());
                if (driver.GetStatus() < 1) Stopped = true;
            }
            catch { Stopped = true; }
        }
        public static byte[] CompileMus(byte[] source, string sourcePath, out byte[] tone, out ushort[] labels, out string error)
        {
            tone = null; labels = null; error = string.Empty;
            try
            {
                muapDotNET.Compiler.Compiler compiler = new(); compiler.Init();
                compiler.SetCompileSwitch(new KeyValuePair<string, string>("SOURCEFILENAME", sourcePath ?? string.Empty));
                using MemoryStream input = new(source, writable: false);
                MmlDatum[] result = compiler.Compile(input, _ => null);
                var info = compiler.GetCompilerInfo();
                if (result == null || info?.errorList?.Count > 0)
                {
                    error = info?.errorList?.Count > 0 ? string.Join(System.Environment.NewLine, info.errorList.Select(item => item.Item3)) : "MUAP compilation failed.";
                    return null;
                }
                if (result.Length > 0 && result[0]?.args != null)
                {
                    tone = result[0].args.OfType<byte[]>().FirstOrDefault();
                    labels = result[0].args.OfType<ushort[]>().FirstOrDefault();
                }
                return result.Select(item => item == null ? (byte)0 : (byte)item.dat).ToArray();
            }
            catch (Exception exception) { error = exception.Message; return null; }
        }
        private void WriteOpna(ChipDatum v) { if (IsRegister(v)) chipRegister.setYM2608Register(0, v.port, v.address, v.data, model, vgmFrameCounter); }
        private void WriteOpn2(ChipDatum v) { if (IsRegister(v)) chipRegister.setYM2612Register(0, v.port, v.address, v.data, model, vgmFrameCounter); }
        private void WriteCs4231(ChipDatum v) { if (IsRegister(v)) chipRegister.setCS4231Register(0, v.port, v.address, v.data, model, vgmFrameCounter); }
        private static bool IsRegister(ChipDatum v) => v != null && v.port >= 0 && v.address >= 0 && v.data >= 0;
        private byte ReadCs4231(byte address) => chipRegister.getCS4231Register(0, address, model, vgmFrameCounter);
        private byte[] GetCs4231Map() => chipRegister.getCS4231EMS_GetCrntMapBuf(0, model);
        private void MapCs4231(byte a, ref byte h, ushort b, ushort d) => chipRegister.setCS4231EMS_Map(0, a, ref h, b, d, model);
        private ushort GetCs4231PageMap() => chipRegister.getCS4231EMS_GetPageMap(0, model);
        private void GetCs4231HandleName(ref byte h, ushort d, ref string s) => chipRegister.getCS4231EMS_GetHandleName(0, ref h, d, ref s, model);
        private void SetCs4231HandleName(ref byte h, ushort d, string s) => chipRegister.setCS4231EMS_SetHandleName(0, ref h, d, s, model);
        private void AllocateCs4231Memory(ref byte h, ref ushort d, ushort b) => chipRegister.setCS4231EMS_AllocMemory(0, ref h, ref d, b, model);
        private sealed class ActionSink : ChipAction
        {
            private readonly Action<ChipDatum> write;
            public ActionSink(Action<ChipDatum> write) => this.write = write;
            public override string GetChipName() => "MDPlayer4Mac";
            public override void WriteRegister(ChipDatum value) => write(value);
            public override void WritePCMData(byte[] data, int startAddress, int endAddress) { }
            public override void WaitSend(long elapsed, int size) { }
        }
    }
}
