using musicDriverInterface;
using mucomDotNET.Common;
using mucomDotNET.Driver;

namespace MDPlayer.Driver.MUCOM
{
    // Thin cross-platform adapter around the official mucomDotNET driver.  MUC source is
    // compiled to an in-memory MUB before this same driver receives it.
    public sealed class MucomDotNET : baseDriver
    {
        public const uint OPNABaseClock = 7_987_200;
        public const uint OPNBBaseClock = 8_000_000;
        public const uint OPMBaseClock = 3_579_545;

        private mucomDotNET.Driver.Driver driver;
        public string SourcePath { get; set; }
        public uint OpmClock { get; private set; } = OPMBaseClock;

        public override GD3 getGD3Info(byte[] buf, uint vgmGd3)
        {
            GD3 result = new();
            try
            {
                GD3Tag tag = new mucomDotNET.Driver.Driver().GetGD3TagInfo(buf);
                if (tag?.dicItem == null) return result;

                result.TrackName = GetTag(tag, enmTag.Title);
                result.TrackNameJ = GetTag(tag, enmTag.TitleJ);
                result.Composer = GetTag(tag, enmTag.Composer);
                result.ComposerJ = GetTag(tag, enmTag.ComposerJ);
                result.VGMBy = GetTag(tag, enmTag.Artist);
                result.Converted = GetTag(tag, enmTag.ReleaseDate);
            }
            catch
            {
                // Driver initialization below is the authoritative validity check. Metadata
                // must not prevent a valid MUB from being played.
            }
            return result;
        }

        public override bool init(byte[] data, ChipRegister register, EnmModel playbackModel,
            EnmChip[] requestedChips, uint requestedLatency, uint requestedWaitTime)
        {
            if (!IsMub(data)) return false;

            GD3 = getGD3Info(data, 0);
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
            OpmClock = GetOpmClock(data);

            try
            {
                driver = new mucomDotNET.Driver.Driver();
                List<ChipAction> actions = new()
                {
                    new MucomChipAction(WriteOpna1, null, null),
                    new MucomChipAction(WriteOpna2, null, null),
                    new MucomChipAction(WriteOpnb1, WriteOpnb1Pcm, null),
                    new MucomChipAction(WriteOpnb2, WriteOpnb2Pcm, null),
                    new MucomChipAction(WriteOpm, null, null),
                };
                MmlDatum[] source = data.Select(value => new MmlDatum(value)).ToArray();
                driver.Init(actions, source, OpenSiblingFile, new object[] { false, true, false, SourcePath ?? string.Empty });
                driver.StartRendering(Common.VGMProcSampleRate, new[]
                {
                    Tuple.Create(string.Empty, (int)OPNABaseClock),
                    Tuple.Create(string.Empty, (int)OPNABaseClock),
                    Tuple.Create(string.Empty, (int)OPNBBaseClock),
                    Tuple.Create(string.Empty, (int)OPNBBaseClock),
                    Tuple.Create(string.Empty, (int)OpmClock),
                });
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
            catch
            {
                Stopped = true;
            }
        }

        public static bool IsMub(byte[] data)
            => data?.Length >= 4
                && ((data[0] == 'M' && data[1] == 'U' && (data[2] == 'C' || data[2] == 'B') && data[3] == '8')
                    || (data[0] == 'm' && data[1] == 'u' && data[2] == 'P' && data[3] == 'b'));

        // Matches the Windows player's MUC path: compile in memory, then feed the resulting
        // MUB bytes to this same driver.  The companion-file callback resolves relative
        // #voice/#pcm/include paths inside the selected source file's folder.
        public static byte[] CompileMuc(byte[] source, string sourcePath, out string error)
        {
            error = string.Empty;
            if (source == null || source.Length == 0)
            {
                error = "MUC source is empty.";
                return null;
            }

            try
            {
                // The upstream compiler's extended-chip scan only examines column zero of
                // each source line.  Traditional MUC files commonly indent a track label
                // (for example "  WXYZ" for OPM), which otherwise makes it emit a normal
                // one-OPNA MUB and silently discard those tracks.  Remove indentation only
                // when the first non-space byte is a legal MUCOM track label; all ordinary
                // MML, tags, comments, and Japanese Shift-JIS text remain byte-identical.
                byte[] normalizedSource = NormalizeTrackDeclarationIndentation(source);

                mucomDotNET.Compiler.Compiler compiler = new();
                compiler.Init();
                using MemoryStream input = new(normalizedSource, writable: false);
                MmlDatum[] compiled = compiler.Compile(input,
                    requestedName => OpenSiblingFile(sourcePath, requestedName));

                CompilerInfo info = compiler.GetCompilerInfo();
                if (compiled == null || info?.errorList?.Count > 0)
                {
                    error = info?.errorList?.Count > 0
                        ? string.Join(Environment.NewLine, info.errorList.Select(item => item.Item3))
                        : "MUCOM88 compilation failed.";
                    return null;
                }

                return compiled.Select(item => item == null ? (byte)0 : (byte)item.dat).ToArray();
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return null;
            }
        }

        // MUB/MUC8 is always one OPNA.  Extended muPb files preserve the same five-chip
        // layout as the Windows player: OPNA #1/#2, OPNB #1/#2, then OPM.
        public static bool[] GetUsedChips(byte[] data)
        {
            bool[] result = new bool[5];
            if (!IsMub(data)) return result;
            if (!(data[0] == 'm' && data[1] == 'u' && data[2] == 'P' && data[3] == 'b'))
            {
                result[0] = true;
                return result;
            }

            try
            {
                MmlDatum[] source = data.Select(value => new MmlDatum(value)).ToArray();
                MUBHeader header = new(source, myEncoding.Default);
                if (header.mupb?.chips == null) return result;

                for (int chip = 0; chip < Math.Min(result.Length, header.mupb.chips.Length); chip++)
                {
                    MupbInfo.ChipDefine definition = header.mupb.chips[chip];
                    result[chip] = definition?.parts?.Any(part =>
                        part?.pages?.Any(page => page?.length > 1) == true) == true;
                }
            }
            catch
            {
                // Let the upstream driver decide whether the malformed data can still be
                // initialized. The conservative fallback prevents a channel-less session.
                result[0] = true;
            }
            return result;
        }

        private static string GetTag(GD3Tag tag, enmTag key)
            => tag.dicItem.TryGetValue(key, out string[] values) && values.Length > 0 ? values[0] : string.Empty;

        public static uint GetOpmClock(byte[] data)
        {
            // MUCOM's extended header stores tags after the binary data.  OPM clock tags are
            // optional; retain the standard OPM clock whenever the header/tag is absent.
            try
            {
                string text = System.Text.Encoding.GetEncoding(932).GetString(data);
                if (text.IndexOf("#opmclockmode x68000", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("#opmclockmode x68k", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("#opmclockmode 4000000", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 4_000_000;
            }
            catch { }
            return OPMBaseClock;
        }

        private Stream OpenSiblingFile(string requestedName)
            => OpenSiblingFile(SourcePath, requestedName);

        private static Stream OpenSiblingFile(string sourcePath, string requestedName)
        {
            try
            {
                string directory = Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(requestedName)) return null;

                string sourceDirectory = Path.GetFullPath(directory);
                string candidate = Path.GetFullPath(Path.Combine(sourceDirectory, requestedName.Replace('\\', Path.DirectorySeparatorChar)));
                string sourcePrefix = sourceDirectory.EndsWith(Path.DirectorySeparatorChar)
                    ? sourceDirectory
                    : sourceDirectory + Path.DirectorySeparatorChar;
                if (!candidate.StartsWith(sourcePrefix, StringComparison.Ordinal) && !string.Equals(candidate, sourceDirectory, StringComparison.Ordinal)) return null;

                return File.Exists(candidate)
                    ? new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read)
                    : null;
            }
            catch { return null; }
        }

        private static byte[] NormalizeTrackDeclarationIndentation(byte[] source)
        {
            const string trackCharacters = "ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmnopqrstuvWXYZwxyz";
            using MemoryStream result = new(source.Length);

            for (int position = 0; position < source.Length;)
            {
                int lineEnd = position;
                while (lineEnd < source.Length && source[lineEnd] != '\r' && source[lineEnd] != '\n') lineEnd++;

                int contentStart = position;
                while (contentStart < lineEnd && (source[contentStart] == (byte)' ' || source[contentStart] == (byte)'\t')) contentStart++;
                bool isTrackDeclaration = contentStart < lineEnd
                    && source[contentStart] < 0x80
                    && trackCharacters.IndexOf((char)source[contentStart]) >= 0;

                int copyStart = isTrackDeclaration ? contentStart : position;
                result.Write(source, copyStart, lineEnd - copyStart);

                if (lineEnd >= source.Length) break;

                // MUCOM's parser deliberately splits source on CRLF.  MUC files copied
                // from a Git repository or authored on macOS often use LF, which would
                // otherwise be parsed as one giant line (and terminate almost at once).
                if (source[lineEnd] == '\r')
                {
                    result.WriteByte((byte)'\r');
                    lineEnd++;
                    if (lineEnd < source.Length && source[lineEnd] == '\n') lineEnd++;
                }
                else
                {
                    lineEnd++;
                    result.WriteByte((byte)'\r');
                }
                result.WriteByte((byte)'\n');
                position = lineEnd;
            }

            return result.ToArray();
        }

        private bool CanWrite(ChipDatum value) => value != null && value.port >= 0 && value.address >= 0 && value.data >= 0;
        private void WriteOpna1(ChipDatum value) { if (CanWrite(value)) chipRegister.setYM2608Register(0, value.port, value.address, value.data, model, vgmFrameCounter); }
        private void WriteOpna2(ChipDatum value) { if (CanWrite(value)) chipRegister.setYM2608Register(1, value.port, value.address, value.data, model, vgmFrameCounter); }
        private void WriteOpnb1(ChipDatum value) { if (CanWrite(value)) chipRegister.setYM2610Register(0, value.port, value.address, value.data, model, vgmFrameCounter); }
        private void WriteOpnb2(ChipDatum value) { if (CanWrite(value)) chipRegister.setYM2610Register(1, value.port, value.address, value.data, model, vgmFrameCounter); }
        private void WriteOpm(ChipDatum value) { if (value != null && value.address >= 0 && value.data >= 0) chipRegister.setYM2151Register(0, value.port, value.address, value.data, model, 0, vgmFrameCounter); }
        private void WriteOpnb1Pcm(byte[] data, int type, int _) { if (data != null) { if (type == 0) chipRegister.WriteYM2610_SetAdpcmA(0, data, model); else chipRegister.WriteYM2610_SetAdpcmB(0, data, model); } }
        private void WriteOpnb2Pcm(byte[] data, int type, int _) { if (data != null) { if (type == 0) chipRegister.WriteYM2610_SetAdpcmA(1, data, model); else chipRegister.WriteYM2610_SetAdpcmB(1, data, model); } }

        private sealed class MucomChipAction : ChipAction
        {
            private readonly Action<ChipDatum> write;
            private readonly Action<byte[], int, int> writePcm;
            private readonly Action<long, int> wait;

            public MucomChipAction(Action<ChipDatum> write, Action<byte[], int, int> writePcm, Action<long, int> wait)
                => (this.write, this.writePcm, this.wait) = (write, writePcm, wait);

            public override string GetChipName() => "MDPlayer4Mac";
            public override void WriteRegister(ChipDatum value) => write?.Invoke(value);
            public override void WritePCMData(byte[] data, int startAddress, int endAddress) => writePcm?.Invoke(data, startAddress, endAddress);
            public override void WaitSend(long elapsed, int size) => wait?.Invoke(elapsed, size);
        }
    }
}
