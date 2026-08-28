// General-purpose "load ANY supported music file and wire it up to MDSound" entry point,
// generalizing VgmEngine.cs (which stays as-is and is still what actually handles VGM/VGZ -
// this file delegates to it for that case) to the other formats MDPlayer supports whose
// driver classes are already ported into this tree: XGM/XGM2 (Sega Genesis), SID/PSID
// (Commodore 64), MND (PC-98), ZMS/ZMD (X68000, via the nise68 sub-emulator), MDX/MDR
// (X68000), NSF (NES/Famicom, incl. expansion audio: MMC5/N106/VRC6/VRC7/FME7), GBS (Game
// Boy), HES (PC Engine), S98 (arcade/PC-98 register-dump, like a simpler VGM), AY (ZX
// Spectrum, needs a real Z80 CPU core - see the AY-specific note below), and ZGM (a niche,
// even-in-the-original-only-partially-implemented format - see LoadZgm).
//
// Every format here follows the same shape VgmEngine.cs established: build a Setting +
// ChipRegister + MDSound.MDSound with the standard null real-hardware stub arrays, construct
// the one relevant baseDriver subclass, call its init(), then wire up the (usually fixed,
// per-platform) chip set as MDSound.MDSound.Chip entries. Where a format's own chip set
// varies per-file (S98, ZGM, and VGM itself) that's built dynamically from what the driver's
// own init() parsed, mirroring VGM's *ClockValue-field pattern.
//
// Simplifications (same philosophy as VgmEngine.cs throughout): single default emulator per
// chip (no alternate emulator backends, no real-hardware output, no dual-chip), and where the
// original picks between multiple PCM sub-chip variants based on Setting flags this always
// picks the same one (documented per format below).
using System.IO;

namespace MDPlayer
{
    public static class MusicEngine
    {
        // Tries magic bytes first (works even if the file is mislabeled/extensionless),
        // falls back to the file extension (fileNameHint may be null, e.g. when a caller only
        // has bytes) for formats that don't have a reliable magic number of their own in this
        // codebase - matching the original Audio.cs's own GetMusic(), which is extension-first
        // for most formats and only falls back to magic-byte sniffing for VGM/VGZ.
        private static EnmFileFormat DetectFormat(byte[] buf, string fileNameHint)
        {
            if (buf.Length >= 2 && buf[0] == 0x1f && buf[1] == 0x8b)
                return EnmFileFormat.VGM; // gzip magic - must be .vgz, the only compressed format here

            if (buf.Length >= 4)
            {
                uint magic4 = Common.getLE32(buf, 0);
                if (magic4 == Vgm.FCC_VGM) return EnmFileFormat.VGM;
                if (magic4 == xgm2.FCC_XGM2) return EnmFileFormat.XGM2;
                if (magic4 == xgm.FCC_XGM) return EnmFileFormat.XGM;
                if (magic4 == Driver.SID.sid.FCC_PSID || magic4 == Driver.SID.sid.FCC_RSID) return EnmFileFormat.SID;
                if (magic4 == hes.FCC_HES) return EnmFileFormat.HES;
                if (magic4 == nsf.FCC_NSF) return EnmFileFormat.NSF;
                if (magic4 == Driver.ZGM.zgm.FCC_ZGM) return EnmFileFormat.ZGM;
            }
            if (buf.Length >= 3 && Common.getLE24(buf, 0) == S98.FCC_S98)
                return EnmFileFormat.S98;

            string ext = string.IsNullOrEmpty(fileNameHint) ? null : Path.GetExtension(fileNameHint).ToLowerInvariant();
            return ext switch
            {
                ".vgm" or ".vgz" => EnmFileFormat.VGM,
                ".xgm" or ".xgz" => EnmFileFormat.XGM,
                ".sid" => EnmFileFormat.SID,
                ".mnd" => EnmFileFormat.MND,
                ".mgs" => EnmFileFormat.MGS,
                ".msd" => EnmFileFormat.MuSICA_src,
                ".bgm" => EnmFileFormat.MuSICA,
                ".nrd" => EnmFileFormat.NRT,
                ".mid" => EnmFileFormat.MID,
                ".rcp" => EnmFileFormat.RCP,
                ".rcs" => EnmFileFormat.RCS,
                ".zms" => EnmFileFormat.ZMS,
                ".zmd" => EnmFileFormat.ZMD,
                ".mdx" => EnmFileFormat.MDX,
                ".mdr" => EnmFileFormat.MDR,
                ".mdl" => EnmFileFormat.MDL,
                ".mub" => EnmFileFormat.MUB,
                ".muc" => EnmFileFormat.MUC,
                ".mml" => EnmFileFormat.MML,
                ".m" or ".m2" or ".mz" => EnmFileFormat.M,
                ".mus" => EnmFileFormat.MUAP_src,
                ".o" or ".ox" or ".oy" => EnmFileFormat.MUAP,
                ".nsf" => EnmFileFormat.NSF,
                ".gbs" => EnmFileFormat.GBS,
                ".hes" => EnmFileFormat.HES,
                ".s98" => EnmFileFormat.S98,
                ".ay" => EnmFileFormat.AY,
                ".zgm" => EnmFileFormat.ZGM,
                _ => EnmFileFormat.unknown,
            };
        }

        // samplingBuffer is MDSound's internal resample buffer size (in frames) - see
        // VgmEngine.Load's identical parameter for the same caveat (unrelated to the caller's
        // per-RenderSamples() chunk size). fileNameHint is only used for extension-based
        // format detection (see DetectFormat) - pass the original file name/path if you have
        // one; it's fine to pass null if all you have is bytes (magic-byte detection still
        // covers VGM/VGZ/XGM/XGM2/SID/HES/NSF/S98/ZGM).
        public static MusicEngineSession Load(byte[] buf, string fileNameHint = null, uint samplingBuffer = 2048)
        {
            if (buf == null || buf.Length < 4) return null;

            EnmFileFormat format = DetectFormat(buf, fileNameHint);
            return format switch
            {
                EnmFileFormat.VGM => VgmEngine.Load(buf, samplingBuffer),
                EnmFileFormat.XGM => LoadXgm(buf, samplingBuffer),
                EnmFileFormat.XGM2 => LoadXgm2(buf, samplingBuffer),
                EnmFileFormat.SID => LoadSid(buf, samplingBuffer),
                EnmFileFormat.MND => LoadMnd(buf, samplingBuffer),
                EnmFileFormat.MGS => LoadMgs(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.MuSICA_src => LoadMusica(buf, samplingBuffer, fileNameHint, compileSource: true),
                EnmFileFormat.MuSICA => LoadMusica(buf, samplingBuffer, fileNameHint, compileSource: false),
                EnmFileFormat.NRT => LoadNrt(buf, samplingBuffer),
                EnmFileFormat.MID => LoadMidi(buf, samplingBuffer),
                EnmFileFormat.RCP => LoadRcp(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.RCS => LoadRcs(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.ZMS => LoadZms(buf, EnmFileFormat.ZMS, samplingBuffer),
                EnmFileFormat.ZMD => LoadZms(buf, EnmFileFormat.ZMD, samplingBuffer),
                EnmFileFormat.MDX => LoadMdx(buf, EnmFileFormat.MDX, samplingBuffer, fileNameHint),
                EnmFileFormat.MDR => LoadMoon(buf, samplingBuffer, fileNameHint, EnmFileFormat.MDR),
                EnmFileFormat.MDL => LoadMoonMdl(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.MUB => LoadMub(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.MUC => LoadMuc(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.MML => LoadPmdMml(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.M => LoadPmd(buf, samplingBuffer, fileNameHint, EnmFileFormat.M),
                EnmFileFormat.MUAP_src => LoadMuapMus(buf, samplingBuffer, fileNameHint),
                EnmFileFormat.MUAP => LoadMuap(buf, samplingBuffer, fileNameHint, null, null, EnmFileFormat.MUAP),
                EnmFileFormat.NSF => LoadNsf(buf, samplingBuffer),
                EnmFileFormat.GBS => LoadGbs(buf, samplingBuffer),
                EnmFileFormat.HES => LoadHes(buf, samplingBuffer),
                EnmFileFormat.S98 => LoadS98(buf, samplingBuffer),
                EnmFileFormat.AY => LoadAy(buf, samplingBuffer),
                EnmFileFormat.ZGM => LoadZgm(buf, samplingBuffer),
                _ => null,
            };
        }

        // Shared boilerplate every format needs: a Setting instance, a ChipRegister with all
        // the real-hardware slots stubbed to null (see VgmEngine.Load's identical
        // construction for why), and an MDSound.MDSound instance. Deliberately duplicated
        // from VgmEngine.Load rather than factored out into a shared helper the two files
        // call - VgmEngine.cs is already sandbox/audio-verified and this keeps that code path
        // completely untouched by this file's changes.
        private static (Setting setting, ChipRegister chipRegister, MDSound.MDSound mds, uint sampleRate) NewCommon(uint samplingBuffer)
        {
            Setting setting = Setting.Load();
            setting.ApplyChipTypeDefaults();
            uint sampleRate = (uint)setting.outputDevice.SampleRate;
            MDSound.MDSound mds = new(sampleRate, samplingBuffer, null);
            ChipRegister chipRegister = new(
                setting
                , null // pianoRollMng
                , mds
                , null // RealChip nScci
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
            return (setting, chipRegister, mds, sampleRate);
        }

        private static MusicEngineSession Finish(
            Setting setting, ChipRegister chipRegister, MDSound.MDSound mds, uint sampleRate,
            uint samplingBuffer, baseDriver driver, System.Collections.Generic.List<MDSound.MDSound.Chip> lstChips,
            EnmFileFormat format, string activeChips)
        {
            if (driver == null || lstChips == null || lstChips.Count == 0) return null;

            chipRegister.initChipRegister(lstChips.ToArray());
            mds.Init(sampleRate, samplingBuffer, lstChips.ToArray());

            return CreateSession(setting, chipRegister, mds, sampleRate, driver, lstChips, format, activeChips);
        }

        // Used by the sequence loaders that must initialize MDSound before their driver
        // uploads ADPCM data or emits its first register writes (MDX and MUB, for example).
        private static MusicEngineSession CreateSession(
            Setting setting, ChipRegister chipRegister, MDSound.MDSound mds, uint sampleRate,
            baseDriver driver, System.Collections.Generic.List<MDSound.MDSound.Chip> lstChips,
            EnmFileFormat format, string activeChips)
        {
            System.Collections.Generic.Dictionary<MDSound.MDSound.enmInstrumentType, uint> chipClocks = new();
            foreach (MDSound.MDSound.Chip c in lstChips) chipClocks[c.type] = c.Clock;
            System.Collections.Generic.Dictionary<MDSound.MDSound.enmInstrumentType, int> chipVolumes = new();
            foreach (MDSound.MDSound.Chip c in lstChips) chipVolumes[c.type] = c.Volume;
            System.Collections.Generic.List<ChipVolumeSlot> chipVolumeSlots = new();
            foreach (MDSound.MDSound.Chip c in lstChips)
            {
                foreach (ChipVolumeKey key in MusicEngineSession.EnumerateMixerSources(c.type, c.ID))
                {
                    bool isWholeChip = key.Component == ChipVolumeComponent.Whole;
                    int initialVolume = isWholeChip
                        ? c.Volume
                        : MusicEngineSession.GetPersistedComponentVolume(setting, key);
                    chipVolumeSlots.Add(new ChipVolumeSlot
                    {
                        Key = key,
                        Volume = initialVolume,
                        // Sequence formats do not carry VGM volume tags. Component gains
                        // therefore reset to their canonical 0 dB default as well.
                        DefaultVolume = isWholeChip ? c.Volume : 0,
                    });

                    if (!isWholeChip)
                    {
                        MusicEngineSession.ApplyComponentVolume(mds, key, initialVolume);
                    }
                }
            }

            return new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = driver,
                SampleRate = sampleRate,
                Format = format,
                ActiveChips = activeChips,
                RenderSamples = (b, off, count) => mds.Update(b, off, count, driver.oneFrameProc),
                ChipClocks = chipClocks,
                ChipVolumes = chipVolumes,
                DefaultChipVolumes = new(chipVolumes),
                ChipVolumeSlots = chipVolumeSlots,
                MasterVolume = setting.balance.MasterVolume,
                DefaultMasterVolume = setting.balance.MasterVolume,
            };
        }

        // NOTE: SamplingRate is deliberately clock/64 (the OPM's real internal sample rate),
        // NOT the output device's sampleRate - matches VgmEngine.cs's VGM YM2151 wiring and
        // Audio.cs's own MdxPlay/MndPlay/ZmdPlay (`chip.SamplingRate = (UInt32)chip.Clock /
        // 64;`). Passing the output sampleRate here instead (an earlier version of this
        // method's bug) makes MDSound resample from the wrong source rate, distorting pitch
        // and speed.
        private static MDSound.MDSound.Chip MakeYM2151(Setting setting, uint clock)
        {
            MDSound.ym2151 ym2151 = new();
            return new MDSound.MDSound.Chip
            {
                type = MDSound.MDSound.enmInstrumentType.YM2151,
                ID = 0,
                Instrument = ym2151,
                Update = ym2151.Update,
                Start = ym2151.Start,
                Stop = ym2151.Stop,
                Reset = ym2151.Reset,
                SamplingRate = clock / 64,
                Volume = setting.balance.YM2151Volume,
                Clock = clock,
                Option = null,
            };
        }

        private static MDSound.MDSound.Chip MakeYM2608(Setting setting, MDSound.ym2608 instrument, int id)
            => new()
            {
                type = MDSound.MDSound.enmInstrumentType.YM2608,
                ID = (byte)id,
                Instrument = instrument,
                Update = instrument.Update,
                Start = instrument.Start,
                Stop = instrument.Stop,
                Reset = instrument.Reset,
                SamplingRate = 55467,
                Volume = setting.balance.YM2608Volume,
                Clock = Driver.MUCOM.MucomDotNET.OPNABaseClock,
                Option = new object[] { (Func<string, Stream>)Common.GetOPNARyhthmStream },
            };

        private static MDSound.MDSound.Chip MakeYM2610(Setting setting, MDSound.ym2610 instrument, int id)
            => new()
            {
                type = MDSound.MDSound.enmInstrumentType.YM2610,
                ID = (byte)id,
                Instrument = instrument,
                Update = instrument.Update,
                Start = instrument.Start,
                Stop = instrument.Stop,
                Reset = instrument.Reset,
                SamplingRate = 55467,
                Volume = setting.balance.YM2610Volume,
                Clock = Driver.MUCOM.MucomDotNET.OPNBBaseClock,
                Option = null,
            };

        // MUCOM88's compiled MUB format uses its own .NET driver to sequence register writes.
        // The original player supports both standard one-OPNA MUBs and extended muPb MUBs;
        // retain that five-target layout here, including the optional second OPNA/OPNB and OPM.
        private static MusicEngineSession LoadMuc(byte[] buf, uint samplingBuffer, string sourcePath)
        {
            byte[] compiledMub = Driver.MUCOM.MucomDotNET.CompileMuc(buf, sourcePath, out _);
            return compiledMub == null
                ? null
                : LoadMub(compiledMub, samplingBuffer, sourcePath, EnmFileFormat.MUC);
        }

        private static MusicEngineSession LoadMub(byte[] buf, uint samplingBuffer, string sourcePath,
            EnmFileFormat format = EnmFileFormat.MUB)
        {
            if (!Driver.MUCOM.MucomDotNET.IsMub(buf)) return null;

            bool[] used = Driver.MUCOM.MucomDotNET.GetUsedChips(buf);
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            MDSound.ym2608 ym2608 = new();
            MDSound.ym2610 ym2610 = new();
            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            var names = new System.Collections.Generic.List<string>();

            if (used[0]) { chips.Add(MakeYM2608(setting, ym2608, 0)); names.Add("YM2608"); }
            if (used[1]) { chips.Add(MakeYM2608(setting, ym2608, 1)); names.Add("YM2608 #2"); }
            if (used[2]) { chips.Add(MakeYM2610(setting, ym2610, 0)); names.Add("YM2610"); }
            if (used[3]) { chips.Add(MakeYM2610(setting, ym2610, 1)); names.Add("YM2610 #2"); }
            if (used[4]) { chips.Add(MakeYM2151(setting, Driver.MUCOM.MucomDotNET.GetOpmClock(buf))); names.Add("YM2151"); }
            if (chips.Count == 0) return null;

            // MUB Init can upload PCM and immediately write registers.  Its targets must
            // therefore exist before the upstream driver starts, just like the Windows path.
            mds.Init(sampleRate, samplingBuffer, chips.ToArray());
            chipRegister.initChipRegister(chips.ToArray());

            Driver.MUCOM.MucomDotNET driver = new() { setting = setting, SourcePath = sourcePath };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0))
                return null;

            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips,
                format, string.Join(" + ", names));
        }

        // PMD's source MML and compiled M/M2/MZ streams share the same OPN/PCM playback
        // path.  The Windows player always creates these four targets, because the source
        // itself decides whether it uses PPZ8, PPSDRV, or P86 companion PCM data.
        private static MusicEngineSession LoadPmdMml(byte[] source, uint samplingBuffer, string sourcePath)
        {
            byte[] compiled = Driver.PMD.PmdDotNET.CompileMml(source, sourcePath, out _);
            return compiled == null ? null : LoadPmd(compiled, samplingBuffer, sourcePath, EnmFileFormat.MML);
        }

        private static MusicEngineSession LoadPmd(byte[] buf, uint samplingBuffer, string sourcePath,
            EnmFileFormat format)
        {
            if (buf == null || buf.Length == 0) return null;
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            MDSound.ym2608 ym2608 = new();
            MDSound.PPZ8 ppz8 = new();
            MDSound.PPSDRV ppsdrv = new();
            MDSound.P86 p86 = new();
            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                MakeYM2608(setting, ym2608, 0),
                MakePmdPcmChip(MDSound.MDSound.enmInstrumentType.PPZ8, ppz8, sampleRate, setting.balance.PPZ8Volume),
                MakePmdPcmChip(MDSound.MDSound.enmInstrumentType.PPSDRV, ppsdrv, sampleRate, 0),
                MakePmdPcmChip(MDSound.MDSound.enmInstrumentType.P86, p86, sampleRate, 0),
            };

            // PMD may load companion PPC/PPS/PZI/P86 data during Init, so register all
            // MDS targets before starting its sequence driver.
            mds.Init(sampleRate, samplingBuffer, chips.ToArray());
            chipRegister.initChipRegister(chips.ToArray());
            Driver.PMD.PmdDotNET driver = new() { setting = setting, SourcePath = sourcePath };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0))
                return null;

            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips, format,
                "YM2608 + PPZ8 + PPSDRV + P86");
        }

        private static MDSound.MDSound.Chip MakePmdPcmChip(MDSound.MDSound.enmInstrumentType type,
            MDSound.Instrument instrument, uint sampleRate, int volume)
            => new()
            {
                type = type,
                ID = 0,
                Instrument = instrument,
                Update = instrument.Update,
                Start = instrument.Start,
                Stop = instrument.Stop,
                Reset = instrument.Reset,
                SamplingRate = sampleRate,
                Volume = volume,
                Clock = Driver.PMD.PmdDotNET.OPNABaseClock,
                Option = null,
            };

        // MGSDRV uses its original Z80 driver program.  The user supplies that program's
        // path in Settings; we only host it and route its AY/OPLL/SCC port writes to MDSound.
        private static MusicEngineSession LoadMgs(byte[] buf, uint samplingBuffer, string sourcePath)
        {
            int terminator = 0;
            while (terminator + 1 < buf.Length && (buf[terminator] != 0x1a || buf[terminator + 1] != 0)) terminator++;
            int tracks = terminator + 7;
            if (tracks < 7 || tracks + 36 > buf.Length) return null;
            int Offset(int index) => buf[tracks + index * 2] | buf[tracks + index * 2 + 1] << 8;
            bool useAy = Offset(0) + Offset(1) + Offset(2) != 0;
            bool useScc = Offset(3) + Offset(4) + Offset(5) + Offset(6) + Offset(7) != 0;
            bool useOpll = Enumerable.Range(8, 10).Select(Offset).Any(value => value != 0);
            if (!useAy && !useScc && !useOpll) return null;

            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            if (string.IsNullOrWhiteSpace(setting.other.MgsDrvPath) || !File.Exists(setting.other.MgsDrvPath)) return null;
            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            var names = new System.Collections.Generic.List<string>();
            if (useAy)
            {
                MDSound.ay8910 ay = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.AY8910, ID = 0, Instrument = ay,
                    Update = ay.Update, Start = ay.Start, Stop = ay.Stop, Reset = ay.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.AY8910Volume, Clock = Driver.MGSDRV.MGSDRV.baseclockAY8910 / 2, Option = null });
                names.Add("AY8910");
            }
            if (useOpll)
            {
                MDSound.emu2413 opll = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2413emu, ID = 0, Instrument = opll,
                    Update = opll.Update, Start = opll.Start, Stop = opll.Stop, Reset = opll.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.YM2413Volume, Clock = Driver.MGSDRV.MGSDRV.baseclockYM2413, Option = null });
                names.Add("YM2413");
            }
            if (useScc)
            {
                MDSound.K051649 scc = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.K051649, ID = 0, Instrument = scc,
                    Update = scc.Update, Start = scc.Start, Stop = scc.Stop, Reset = scc.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.K051649Volume, Clock = Driver.MGSDRV.MGSDRV.baseclockK051649, Option = null });
                names.Add("SCC");
            }
            mds.Init(sampleRate, samplingBuffer, chips.ToArray()); chipRegister.initChipRegister(chips.ToArray());
            Driver.MGSDRV.MGSDRV driver = new()
            {
                setting = setting,
                PlayingFileName = sourcePath,
                DriverFilePath = setting.other.MgsDrvPath,
            };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0)) return null;
            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips, EnmFileFormat.MGS, string.Join(" + ", names));
        }

        // MuSICA uses an MSX-resident player program.  Its compiled BGM data has a fixed
        // 17-entry track table: OPLL 0..8, AY 9..11, SCC 12..16.  .msd is compiled first
        // with the user's KINROU4.COM; .bgm is already compiled and only needs KINROU5.DRV.
        private static MusicEngineSession LoadMusica(byte[] source, uint samplingBuffer, string sourcePath, bool compileSource)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            byte[] buf = source;
            if (compileSource)
            {
                if (string.IsNullOrWhiteSpace(setting.other.MusicaCompilerPath) || !File.Exists(setting.other.MusicaCompilerPath)) return null;
                byte[] vcd = null;
                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    string vcdPath = Path.ChangeExtension(sourcePath, ".vcd");
                    if (File.Exists(vcdPath)) vcd = File.ReadAllBytes(vcdPath);
                }
                Driver.MuSICA.MuSICA_K4 compiler = new() { CompilerFilePath = setting.other.MusicaCompilerPath };
                if (!compiler.Compile(source, vcd)) return null;
                buf = compiler.GetBgmBin();
                if (buf == null) return null;
            }

            if (buf.Length < 42 || string.IsNullOrWhiteSpace(setting.other.MusicaDriverPath) || !File.Exists(setting.other.MusicaDriverPath)) return null;
            int Offset(int index) => buf[8 + index * 2] | buf[9 + index * 2] << 8;
            bool useOpll = Enumerable.Range(0, 9).Select(Offset).Any(value => value != 0);
            bool useAy = Enumerable.Range(9, 3).Select(Offset).Any(value => value != 0);
            bool useScc = Enumerable.Range(12, 5).Select(Offset).Any(value => value != 0);
            if (!useOpll && !useAy && !useScc) return null;

            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            var names = new System.Collections.Generic.List<string>();
            if (useAy)
            {
                MDSound.ay8910 ay = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.AY8910, ID = 0, Instrument = ay,
                    Update = ay.Update, Start = ay.Start, Stop = ay.Stop, Reset = ay.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.AY8910Volume, Clock = Driver.MuSICA.MuSICA.baseclockAY8910 / 2, Option = null });
                names.Add("AY8910");
            }
            if (useOpll)
            {
                MDSound.emu2413 opll = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2413emu, ID = 0, Instrument = opll,
                    Update = opll.Update, Start = opll.Start, Stop = opll.Stop, Reset = opll.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.YM2413Volume, Clock = Driver.MuSICA.MuSICA.baseclockYM2413, Option = null });
                names.Add("YM2413");
            }
            if (useScc)
            {
                MDSound.K051649 scc = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.K051649, ID = 0, Instrument = scc,
                    Update = scc.Update, Start = scc.Start, Stop = scc.Stop, Reset = scc.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.K051649Volume, Clock = Driver.MuSICA.MuSICA.baseclockK051649, Option = null });
                names.Add("SCC");
            }
            mds.Init(sampleRate, samplingBuffer, chips.ToArray());
            chipRegister.initChipRegister(chips.ToArray());
            if (useOpll) chipRegister.setYM2413Register(0, 14, 32, EnmModel.VirtualModel, 0);
            Driver.MuSICA.MuSICA driver = new() { setting = setting, PlayingFileName = sourcePath, DriverFilePath = setting.other.MusicaDriverPath };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel,
                new[] { EnmChip.AY8910, EnmChip.YM2413, EnmChip.K051649 }, 0, 0)) return null;
            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips,
                compileSource ? EnmFileFormat.MuSICA_src : EnmFileFormat.MuSICA, string.Join(" + ", names));
        }

        // NRTDRV is fully managed and embeds its player logic in the data driver.  It can use
        // one/two YM2151 chips and AY8910; its own parser reports exactly which combination
        // the current NRD file needs before we create the MDSound graph.
        private static MusicEngineSession LoadNrt(byte[] buf, uint samplingBuffer)
        {
            if (buf.Length < 42) return null;
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            NRTDRV driver = new(setting) { setting = setting };
            int use = driver.checkUseChip(buf);
            bool useOpm0 = (use & 3) != 0;
            bool useOpm1 = (use & 2) != 0;
            bool useAy = (use & 4) != 0;
            if (!useOpm0 && !useAy) return null;

            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            var names = new System.Collections.Generic.List<string>();
            MDSound.ym2151 opm = null;
            for (int id = 0; id < 2; id++)
            {
                if ((id == 0 && !useOpm0) || (id == 1 && !useOpm1)) continue;
                opm ??= new MDSound.ym2151();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2151, ID = (byte)id, Instrument = opm,
                    Update = opm.Update, Start = opm.Start, Stop = opm.Stop, Reset = opm.Reset, SamplingRate = 4_000_000 / 64,
                    Volume = setting.balance.YM2151Volume, Clock = 4_000_000, Option = null });
                names.Add("YM2151");
            }
            if (useAy)
            {
                MDSound.ay8910 ay = new();
                chips.Add(new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.AY8910, ID = 0, Instrument = ay,
                    Update = ay.Update, Start = ay.Start, Stop = ay.Stop, Reset = ay.Reset, SamplingRate = sampleRate,
                    Volume = setting.balance.AY8910Volume, Clock = 2_000_000 / 2, Option = null });
                names.Add("AY8910");
            }
            mds.Init(sampleRate, samplingBuffer, chips.ToArray());
            chipRegister.initChipRegister(chips.ToArray());
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.YM2151, EnmChip.AY8910 }, 0, 0)) return null;
            driver.Call(0);
            driver.Call(1);
            string active = string.Join(" + ", names.GroupBy(name => name).Select(group => group.Count() > 1 ? $"{group.Count()}x{group.Key}" : group.Key));
            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips, EnmFileFormat.NRT, active);
        }

        // Standard MIDI and Recomposer output is not a register-dump chip stream. Their
        // original sequence drivers still provide parsing, timing, loops and SysEx; this
        // adapter routes emitted bytes to macOS's built-in multitimbral DLS GM synth and
        // renders the synth's PCM through the normal Audio Queue path.
        private static MusicEngineSession CreateMidiSession(Setting setting, ChipRegister chipRegister,
            MDSound.MDSound mds, uint sampleRate, baseDriver driver, EnmFileFormat format, string activeChips)
        {
            MacMidiSynth synth;
            try
            {
                synth = new MacMidiSynth(sampleRate);
            }
            catch
            {
                return null;
            }
            chipRegister.SetMidiMessageSink((_, data) => synth.Send(data));

            return new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = driver,
                SampleRate = sampleRate,
                Format = format,
                ActiveChips = activeChips,
                RenderSamples = (b, off, count) =>
                {
                    int frames = count / 2;
                    for (int frame = 0; frame < frames; frame++)
                    {
                        synth.SetFrameOffset(frame);
                        driver.oneFrameProc();
                    }
                    return synth.Render(b, off, frames * 2);
                },
                MasterVolume = setting.balance.MasterVolume,
                DefaultMasterVolume = setting.balance.MasterVolume,
            };
        }

        private static MusicEngineSession LoadMidi(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            MID driver = new() { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0)) return null;
            return CreateMidiSession(setting, chipRegister, mds, sampleRate, driver, EnmFileFormat.MID,
                "General MIDI (macOS DLS Synth)");
        }

        private static MusicEngineSession LoadRcp(byte[] buf, uint samplingBuffer, string sourcePath)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            RCP driver = new() { setting = setting, ExtendFile = LoadRcpControlFiles(buf, sourcePath) };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0)) return null;
            return CreateMidiSession(setting, chipRegister, mds, sampleRate, driver, EnmFileFormat.RCP,
                "Recomposer MIDI (macOS DLS Synth)");
        }

        // RCP files optionally name CM6/GSD setup data in their headers. The original driver
        // expands those into SysEx before the sequence starts, so preserve that behavior when
        // the companion files are located beside the selected RCP file.
        private static System.Collections.Generic.List<Tuple<string, byte[]>> LoadRcpControlFiles(byte[] rcp, string sourcePath)
        {
            var files = new System.Collections.Generic.List<Tuple<string, byte[]>>();
            if (string.IsNullOrWhiteSpace(sourcePath)) return files;
            string directory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(directory)) return files;
            RCP.getControlFileName(rcp, out string cm6, out string gsd, out string gsd2);
            AddMidiCompanionFiles(files, directory, new[] { cm6, gsd, gsd2 });
            return files;
        }

        private static System.Collections.Generic.List<Tuple<string, byte[]>> LoadRcsCompanionFiles(byte[] rcs, string sourcePath)
        {
            var files = new System.Collections.Generic.List<Tuple<string, byte[]>>();
            if (string.IsNullOrWhiteSpace(sourcePath)) return files;
            string directory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(directory)) return files;

            // RCS contains PCM8 data itself but names an RCP sequence beside it; that RCP
            // can in turn name CM6/GSD tone-module setup files. Give the driver all of them
            // as in-memory companions, so it neither depends on the current working folder
            // nor loses the original setup SysEx stream.
            RCS.getControlFileName(sourcePath, null, rcs, out string rcp, out string cm6, out string gsd, out string gsd2);
            AddMidiCompanionFiles(files, directory, new[] { rcp, cm6, gsd, gsd2 });
            return files;
        }

        private static void AddMidiCompanionFiles(System.Collections.Generic.List<Tuple<string, byte[]>> files,
            string directory, IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                // Companion fields are stored inside music data. Resolve only a sibling of
                // the file the user explicitly opened; never let a path in the sequence walk
                // outside that directory.
                string siblingName = Path.GetFileName(name.Trim().Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(siblingName)) continue;
                string path = Path.Combine(directory, siblingName);
                if (!File.Exists(path)) continue;
                string extension = Path.GetExtension(path).ToUpperInvariant();
                files.Add(Tuple.Create(extension, File.ReadAllBytes(path)));
            }
        }

        // RCS is a Recomposer sequence accompanied by embedded X68000 PCM8 samples. Its
        // MIDI tracks go to DLS while its PCM8 stream remains in MDSound, then the two PCM
        // buffers are mixed before handing audio to CoreAudio.
        private static MusicEngineSession LoadRcs(byte[] buf, uint samplingBuffer, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return null;
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            MDSound.PCM8PP pcm8 = new();
            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                new()
                {
                    type = MDSound.MDSound.enmInstrumentType.PCM8PP,
                    ID = 0,
                    Instrument = pcm8,
                    Update = pcm8.Update,
                    Start = pcm8.Start,
                    Stop = pcm8.Stop,
                    Reset = pcm8.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.PCM8Volume,
                    Clock = 4_000_000,
                    Option = null,
                },
            };
            mds.Init(sampleRate, samplingBuffer, chips.ToArray());
            chipRegister.initChipRegister(chips.ToArray());

            MacMidiSynth synth;
            try
            {
                synth = new MacMidiSynth(sampleRate);
            }
            catch
            {
                return null;
            }
            chipRegister.SetMidiMessageSink((_, data) => synth.Send(data));
            RCS driver = new()
            {
                setting = setting,
                filename = sourcePath,
                ExtendFile = LoadRcsCompanionFiles(buf, sourcePath),
                pcm8type = 1,
                pcm8pp = pcm8,
            };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0)) return null;

            MusicEngineSession session = CreateSession(setting, chipRegister, mds, sampleRate, driver, chips,
                EnmFileFormat.RCS, "PCM8 + Recomposer MIDI (macOS DLS Synth)");
            short[] synthBuffer = Array.Empty<short>();
            session.RenderSamples = (b, off, count) =>
            {
                int frame = 0;
                int written = mds.Update(b, off, count, () =>
                {
                    synth.SetFrameOffset(frame++);
                    driver.oneFrameProc();
                });
                if (written <= 0) return written;
                if (synthBuffer.Length < written) synthBuffer = new short[written];
                Array.Clear(synthBuffer, 0, written);
                synth.Render(synthBuffer, 0, written);
                for (int i = 0; i < written; i++)
                {
                    b[off + i] = (short)Math.Clamp(b[off + i] + synthBuffer[i], short.MinValue, short.MaxValue);
                }
                return written;
            };
            return session;
        }

        private static MusicEngineSession LoadMoonMdl(byte[] source, uint samplingBuffer, string sourcePath)
        {
            byte[] compiled = Driver.Moon.MoonDotNET.CompileMdl(source, sourcePath, out _);
            return compiled == null ? null : LoadMoon(compiled, samplingBuffer, sourcePath, EnmFileFormat.MDL);
        }

        // MoonDriver's compiled MDR header selects either OPL4 (YMF278B) or OPL3 (YMF262).
        // Unlike the old placeholder path, MDR is not an MDX/YM2151 sequence.
        private static MusicEngineSession LoadMoon(byte[] buf, uint samplingBuffer, string sourcePath,
            EnmFileFormat format)
        {
            if (buf == null || buf.Length < 8) return null;
            bool useOpl3 = (buf[7] & 2) != 0 && (buf[7] & 1) != 0;
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            string chipName;
            if (useOpl3)
            {
                MDSound.ymf262 opl3 = new();
                chips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YMF262, ID = 0, Instrument = opl3,
                    Update = opl3.Update, Start = opl3.Start, Stop = opl3.Stop, Reset = opl3.Reset,
                    SamplingRate = sampleRate, Volume = setting.balance.YMF262Volume, Clock = 14_318_180,
                    Option = new object[] { Common.GetApplicationFolder() },
                });
                chipName = "YMF262 (OPL3)";
            }
            else
            {
                MDSound.ymf278b opl4 = new();
                chips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YMF278B, ID = 0, Instrument = opl4,
                    Update = opl4.Update, Start = opl4.Start, Stop = opl4.Stop, Reset = opl4.Reset,
                    SamplingRate = sampleRate, Volume = setting.balance.YMF278BVolume, Clock = 33_868_800,
                    Option = new object[] { Common.GetApplicationFolder() },
                });
                chipName = "YMF278B (OPL4)";
            }

            mds.Init(sampleRate, samplingBuffer, chips.ToArray());
            chipRegister.initChipRegister(chips.ToArray());
            Driver.Moon.MoonDotNET driver = new() { setting = setting, SourcePath = sourcePath, UseOpl3 = useOpl3 };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0)) return null;
            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips, format, chipName);
        }

        private static MusicEngineSession LoadMuapMus(byte[] source, uint samplingBuffer, string sourcePath)
        {
            byte[] compiled = Driver.MUAP.MuapDotNET.CompileMus(source, sourcePath, out byte[] tone, out ushort[] labels, out _);
            return compiled == null ? null : LoadMuap(compiled, samplingBuffer, sourcePath, tone, labels, EnmFileFormat.MUAP_src);
        }

        // MUAP98 uses an OPNA, an OPN2-compatible YM2612 target, and CS4231 PCM.  The
        // driver exchanges CS4231 FIFO/EMS state through ChipRegister, as in the Windows path.
        private static MusicEngineSession LoadMuap(byte[] buf, uint samplingBuffer, string sourcePath,
            byte[] tone, ushort[] labels, EnmFileFormat format)
        {
            if (buf == null || buf.Length == 0) return null;
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            MDSound.ym2608 opna = new(); MDSound.ym2612 opn2 = new(); MDSound.CS4231 cs4231 = new();
            var chips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                MakeYM2608(setting, opna, 0),
                new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2612, ID = 0, Instrument = opn2,
                    Update = opn2.Update, Start = opn2.Start, Stop = opn2.Stop, Reset = opn2.Reset,
                    SamplingRate = sampleRate, Volume = setting.balance.YM2612Volume, Clock = 7_987_200, Option = null },
                new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.CS4231, ID = 0, Instrument = cs4231,
                    Update = cs4231.Update, Start = cs4231.Start, Stop = cs4231.Stop, Reset = cs4231.Reset,
                    SamplingRate = 55_467, Volume = setting.balance.CS4231Volume, Clock = 0, Option = null },
            };
            mds.Init(sampleRate, samplingBuffer, chips.ToArray()); chipRegister.initChipRegister(chips.ToArray());
            Driver.MUAP.MuapDotNET driver = new() { setting = setting, SourcePath = sourcePath, ToneBuffer = tone, LabelAddresses = labels };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new[] { EnmChip.Unuse }, 0, 0)) return null;
            return CreateSession(setting, chipRegister, mds, sampleRate, driver, chips, format, "YM2608 + YM2612 + CS4231");
        }

        // Sega Genesis/Mega Drive's fixed chip set - same clocks Audio.cs's XgmPlay/Xgm2Play
        // hard-code (identical to the values real Sega Genesis VGMs use for these two chips).
        private static System.Collections.Generic.List<MDSound.MDSound.Chip> MakeGenesisChips(Setting setting, uint sampleRate)
        {
            MDSound.ym2612 ym2612 = new();
            MDSound.sn76489 sn76489 = new();
            return new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2612,
                    ID = 0,
                    Instrument = ym2612,
                    Update = ym2612.Update,
                    Start = ym2612.Start,
                    Stop = ym2612.Stop,
                    Reset = ym2612.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YM2612Volume,
                    Clock = 7670454,
                    Option = null,
                },
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.SN76489,
                    ID = 0,
                    Instrument = sn76489,
                    Update = sn76489.Update,
                    Start = sn76489.Start,
                    Stop = sn76489.Stop,
                    Reset = sn76489.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.SN76489Volume,
                    Clock = 3579545,
                    Option = null,
                },
            };
        }

        private static MusicEngineSession LoadXgm(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            xgm driver = new(setting) { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;
            var lstChips = MakeGenesisChips(setting, sampleRate);
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.XGM, "YM2612=7670454Hz SN76489=3579545Hz");
        }

        private static MusicEngineSession LoadXgm2(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            xgm2 driver = new(setting) { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;
            var lstChips = MakeGenesisChips(setting, sampleRate);
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.XGM2, "YM2612=7670454Hz SN76489=3579545Hz");
        }

        // SID (Commodore 64) is a fundamentally different shape from every other format here:
        // it doesn't go through ChipRegister/MDSound.MDSound.Chip at all. sid.cs runs a real
        // MOS 6510 CPU + SID chip emulation (libsidplayfp, already fully ported - see
        // macos/README.md) and produces PCM samples directly via its own Render(short[],
        // uint) method, bypassing MDSound.MDSound.Update()/oneFrameProc entirely. That's why
        // MusicEngineSession.RenderSamples is a delegate rather than always being
        // `mds.Update(...)` - this is the one format that needs the other branch.
        // Also unlike every other format, SID's Stopped flag is never set by the driver
        // itself (many SID tunes loop forever with no defined end) - callers rely on
        // RenderSamples always returning `count` and their own duration cap (EngineSmokeTest's
        // safetyLimitChunks, currently ~60s) to end playback.
        private static MusicEngineSession LoadSid(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);

            Driver.SID.sid driver = new() { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;

            return new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = driver,
                SampleRate = sampleRate,
                Format = EnmFileFormat.SID,
                ActiveChips = "SID (MOS 6581/8580, via libsidplayfp)",
                RenderSamples = (b, off, count) =>
                {
                    // sid.Render always fills the buffer starting at index 0 - if a caller
                    // ever passes a nonzero offset (none of EngineSmokeTest/LivePlayer/
                    // MDPlayerUI do today) fall back through a temp buffer instead of
                    // silently writing to the wrong place.
                    if (off == 0)
                        return (int)driver.Render(b, (uint)count);
                    short[] tmp = new short[count];
                    uint written = driver.Render(tmp, (uint)count);
                    System.Array.Copy(tmp, 0, b, off, written);
                    return (int)written;
                },
                MasterVolume = setting.balance.MasterVolume,
                DefaultMasterVolume = setting.balance.MasterVolume,
            };
        }

        // PC-98's typical dual-OPM+OPNA setup: YM2151 (the "main" FM chip on many PC-98
        // sound boards) plus YM2608/OPNA, matching Audio.cs's MndPlay fixed clocks exactly
        // (8000000 for YM2608, not the more common 7987200 - the original's own comment
        // flags this same discrepancy, so it's not something this port introduced). MND
        // files can also carry X68000-style ADPCM samples (mpcmX68k/mpcmpp in the original,
        // gated by a Setting flag choosing between them) - this always wires mpcmpp, the
        // simpler of the two, matching this project's single-default-emulator philosophy.
        private static MusicEngineSession LoadMnd(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            Driver.MNDRV.mndrv driver = new() { setting = setting, ExtendFile = null };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;

            MDSound.ym2608 ym2608 = new();
            MDSound.mpcmpp mpcmpp = new();
            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                MakeYM2151(setting, 4000000),
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2608,
                    ID = 0,
                    Instrument = ym2608,
                    Update = ym2608.Update,
                    Start = ym2608.Start,
                    Stop = ym2608.Stop,
                    Reset = ym2608.Reset,
                    SamplingRate = 55467, // matches Audio.cs - OPNA's rhythm mixer runs at a fixed rate (not sampleRate)
                    Volume = setting.balance.YM2608Volume,
                    Clock = 8000000,
                    Option = new object[] { (Func<string, Stream>)Common.GetOPNARyhthmStream },
                },
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.mpcmpp,
                    ID = 0,
                    Instrument = mpcmpp,
                    Update = mpcmpp.Update,
                    Start = mpcmpp.Start,
                    Stop = mpcmpp.Stop,
                    Reset = mpcmpp.Reset,
                    SamplingRate = sampleRate,
                    Volume = 0, // matches Audio.cs's MndPlay literal (no dedicated mpcmpp slider)
                    Clock = 15600,
                    Option = new object[] { Common.GetApplicationFolder() },
                },
            };
            // mndrv.cs's own register-routing code writes ADPCM output through whichever of
            // these back-references is set (see its _mpcm_* methods) - without this the PCM
            // channel's register writes go nowhere and MND files with PCM samples play FM-only.
            driver.mpcmpp = mpcmpp;
            driver.mpcmtype = 1; // 1 = mpcmpp, matches Audio.cs's MndPlay (0 would be mpcmX68k)
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.MND, "YM2151=4000000Hz YM2608=8000000Hz mpcmpp=15600Hz");
        }

        // ZMS/ZMD (X68000's "Zmusic" driver format) is played by nise68, a full X68000
        // environment emulator (68000 CPU + Human68k OS calls - see Driver/ZMS/nise68/) that
        // runs the actual Zmusic driver program, which in turn talks to the X68000's real
        // YM2151 + ADPCM hardware. Despite that complexity under the hood, the chip set it
        // needs at the MDSound level is the same simple YM2151+PCM pair as MDX (see
        // LoadMdx) - matches Audio.cs's ZmdPlay fixed clocks.
        private static MusicEngineSession LoadZms(byte[] buf, EnmFileFormat format, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            Driver.ZMS.ZMS driver = new(format) { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;

            MDSound.mpcmpp mpcmpp = new();
            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                MakeYM2151(setting, 4000000),
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.mpcmpp,
                    ID = 0,
                    Instrument = mpcmpp,
                    Update = mpcmpp.Update,
                    Start = mpcmpp.Start,
                    Stop = mpcmpp.Stop,
                    Reset = mpcmpp.Reset,
                    SamplingRate = sampleRate,
                    Volume = 0, // matches Audio.cs's ZmdPlay literal (no dedicated mpcmpp slider)
                    Clock = 15600,
                    Option = null,
                },
            };
            // Same reasoning as LoadMnd's driver.mpcmpp/mpcmtype back-reference - ZMS.cs's own
            // register routing needs to know where to send ADPCM output.
            driver.mpcmpp = mpcmpp;
            driver.mpcmtype = 1;
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                format, "YM2151=4000000Hz mpcmpp=15600Hz");
        }

        // X68000's MXDRV format is architecturally unlike every other chip-register-driven
        // format here (MND/ZMS/MDX's siblings): MXDRV.cs runs a full X68000 IOCS-level sound
        // driver (MDSound.NX68Sound.X68Sound/sound_iocs, via a MDSound.ym2151_x68sound
        // instance called `mdxPCM`) that renders BOTH the OPM and the PCM8 ADPCM channel
        // itself and hands back finished PCM through its own Render().  The Windows mixer
        // then calls mds.Update() with its increment flag set so that this already-rendered
        // PCM is preserved while the normal MDSound output path is mixed in. The MDSound
        // chip entry
        // below (Update/Start/Stop/Reset all null) exists only so ChipRegister/MDSound.Init
        // have a non-empty chip list to register - matching Audio.cs's MdxPlay, which adds
        // the exact same null-delegate placeholder chip for its `mdxPCM_V` instrument.
        // Audio.cs also supports an alternate PCM8PP-chip-based path when
        // `setting.mxdrv.pcm8type != 0`, but its own comment above the default branch says
        // "mxdrvは特殊で必ずPCM8が必要" (MXDRV is special, it always needs [the built-in] PCM8) -
        // pcm8type 0 (mdxPCM's own built-in PCM8, no separate PCM8PP chip) is upstream's own
        // default, so using it here isn't a simplification this port introduced.
        // MDR is the same driver/chip set under a slightly different file variant.
        // MDX can refer to an external PDX sample bank by name. Resolve only a sibling file
        // beside the selected MDX/MDR file: this mirrors the X68000 convention and avoids
        // treating a path encoded in music data as permission to read arbitrary locations.
        // Callers that have only bytes (and therefore no source path) still support FM-only
        // MDX files; MXDRV reports its normal initialization failure when a required PDX is
        // unavailable.
        private static Tuple<string, byte[]>? TryLoadMdxPdx(byte[] mdxBytes, string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return null;

            try
            {
                Driver.MXDRV.MXDRV.GetPDXFileName(mdxBytes, out string pdxName);
                if (string.IsNullOrWhiteSpace(pdxName)) return null;

                string? directory = Path.GetDirectoryName(sourcePath);
                string siblingName = Path.GetFileName(pdxName.Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(siblingName)) return null;

                // Many MDX files store the PDX *stem* rather than its full filename.
                // The Windows player therefore tries both `PDX` and `PDX + ".PDX"`
                // (frmMain.cs:getExtendFileAllBytes). Mirroring that fallback is essential:
                // without it, ordinary MDX+PDX pairs fail Init() and appear in the UI as
                // an unsupported format.
                string[] candidateNames = { siblingName, siblingName + ".PDX" };
                string? siblingPath = null;
                foreach (string candidateName in candidateNames.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string candidatePath = Path.Combine(directory, candidateName);
                    if (!File.Exists(candidatePath)) continue;

                    siblingPath = candidatePath;
                    break;
                }
                if (siblingPath == null) return null;

                return Tuple.Create(siblingName, File.ReadAllBytes(siblingPath));
            }
            catch
            {
                // A malformed MDX should be handled by MXDRV's own parser. Do not turn
                // optional companion discovery into an unrelated load exception.
                return null;
            }
        }

        private static MusicEngineSession LoadMdx(byte[] buf, EnmFileFormat format, uint samplingBuffer, string? sourcePath)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);

            MDSound.ym2151_x68sound mdxPCM = new();
            mdxPCM.x68sound[0] = new MDSound.NX68Sound.X68Sound();
            mdxPCM.sound_Iocs[0] = new MDSound.NX68Sound.sound_iocs(mdxPCM.x68sound[0]);

            // MXDRV uses two distinct paths, just as Audio.cs:MdxPlay does:
            //   1. OPM register writes go through ChipRegister into a normal YM2151 chip.
            //   2. The X68Sound instance renders the PCM8/ADPCM stream directly.
            // The previous port kept only (2), so the channel view showed PCM8 alone and
            // there was no YM2151 instrument for MXDRV's OPM_SUB register writes.
            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                MakeYM2151(setting, 4_000_000),
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2151x68soundPCM,
                    ID = 0,
                    Instrument = mdxPCM,
                    Update = null,
                    Start = null,
                    Stop = null,
                    Reset = null,
                    // This placeholder represents MXDRV's separate PCM8/ADPCM output,
                    // rather than the preceding YM2151 FM chip.
                    Volume = setting.balance.PCM8Volume,
                    Clock = 4000000,
                },
            };

            Driver.MXDRV.MXDRV driver = new()
            {
                setting = setting,
                ExtendFile = TryLoadMdxPdx(buf, sourcePath),
                pcm8type = 0,
            };
            // MdxPlay initializes MDSound and ChipRegister before starting MXDRV.  That
            // order matters now that OPM_SUB has a real YM2151 target for its writes.
            chipRegister.initChipRegister(lstChips.ToArray());
            mds.Init(sampleRate, samplingBuffer, lstChips.ToArray());

            if (!driver.Init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0, mdxPCM, null))
                return null;

            bool pcmBankMissing = !string.IsNullOrEmpty(driver.errMsg);

            MusicEngineSession? mdxSession = null;
            mdxSession = new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = driver,
                SampleRate = sampleRate,
                Format = format,
                ActiveChips = pcmBankMissing
                    ? "YM2151 (PDX 미발견 — PCM8/ADPCM은 음소거됨)"
                    : "YM2151+PCM8 (X68000 IOCS sound driver, via MXDRV/NX68Sound)",
                // LoadMdx has a custom render delegate instead of going through Finish(),
                // so carry across the YM2151 clock metadata explicitly. Channel-view
                // creation is correctly keyed from this map and will now draw the OPM view.
                ChipClocks = new()
                {
                    [MDSound.MDSound.enmInstrumentType.YM2151] = 4_000_000,
                },
                ChipVolumes = new()
                {
                    [MDSound.MDSound.enmInstrumentType.YM2151] = setting.balance.YM2151Volume,
                    [MDSound.MDSound.enmInstrumentType.YM2151x68soundPCM] = setting.balance.PCM8Volume,
                },
                DefaultChipVolumes = new()
                {
                    [MDSound.MDSound.enmInstrumentType.YM2151] = setting.balance.YM2151Volume,
                    [MDSound.MDSound.enmInstrumentType.YM2151x68soundPCM] = setting.balance.PCM8Volume,
                },
                ChipVolumeSlots = new()
                {
                    new ChipVolumeSlot
                    {
                        Key = new ChipVolumeKey(MDSound.MDSound.enmInstrumentType.YM2151, 0),
                        Volume = setting.balance.YM2151Volume,
                        DefaultVolume = setting.balance.YM2151Volume,
                    },
                },
                HasDirectPcmVolume = !pcmBankMissing,
                DirectPcmVolume = setting.balance.PCM8Volume,
                DefaultDirectPcmVolume = setting.balance.PCM8Volume,
                RenderSamples = (b, off, count) =>
                {
                    // MXDRV.Render's return value is not an end-of-song indication. In
                    // particular, X68Sound may return zero after successfully filling the
                    // requested samples. The original Audio.cs intentionally ignores it,
                    // always mixes the produced PCM, and reports the whole buffer consumed.
                    // Treating zero as EOF stopped MDX playback immediately on macOS.
                    mds.setIncFlag();
                    for (int i = 0; i < count; i += 2)
                    {
                        int n = System.Math.Min(2, count - i);
                        if (n < 2)
                            break; // MDSound's stereo mixer requires complete frames.

                        driver.Render(b, off + i, n);
                        // MXDRV starts X68Sound with OPM output disabled; its buffer is the
                        // PDX-backed PCM8/ADPCM stream, while MDSound adds the OPM below.
                        // Scale this stream first to keep the ADPCM fader independent.
                        if (mdxSession!.DirectPcmVolume != 0)
                        {
                            double gain = System.Math.Pow(10.0, mdxSession.DirectPcmVolume / 40.0);
                            for (int sample = 0; sample < n; sample++)
                            {
                                int index = off + i + sample;
                                b[index] = (short)System.Math.Clamp(
                                    (int)System.Math.Round(b[index] * gain), short.MinValue, short.MaxValue);
                            }
                        }
                        mds.Update(b, off + i, n, null);
                    }
                    return count;
                },
                MasterVolume = setting.balance.MasterVolume,
                DefaultMasterVolume = setting.balance.MasterVolume,
            };

            if (!pcmBankMissing)
            {
                // PCM8 is MXDRV's eight-voice ADPCM/sample stream sourced by the companion
                // PDX bank, not the OPM FM output above. Do not show a meaningless fader
                // for MDX files whose PDX companion was not found.
                mdxSession.ChipVolumeSlots.Add(new ChipVolumeSlot
                {
                    Key = new ChipVolumeKey(MDSound.MDSound.enmInstrumentType.YM2151x68soundPCM, 0),
                    Volume = setting.balance.PCM8Volume,
                    DefaultVolume = setting.balance.PCM8Volume,
                });
            }
            return mdxSession;
        }

        // NES/Famicom (NSF). Unlike VGM's NES block (VgmEngine.cs, which drives a MDSound.
        // nes_intf-owned chip purely from VGM register-write commands), NSF files are real
        // 6502 machine code: nsf.cs's own init() (via its private nsfInit()) builds a
        // completely separate, self-contained 6502 CPU + NES APU/DMC/FDS/expansion-audio
        // emulator directly on `chipRegister` (chipRegister.nes_cpu/nes_apu/nes_dmc/nes_fds/
        // nes_n106/nes_vrc6/nes_mmc5/nes_fme7/nes_vrc7 - all MDSound.np.* classes, NOT
        // MDSound.nes_intf) and produces finished PCM samples itself via Render(), executing
        // real CPU instructions to drive the chips exactly like actual NES hardware would.
        // This bypasses MDSound.MDSound.Chip.Update()/ChipRegister's register-write dispatch
        // entirely - matching Audio.cs's TrdVgmVirtualMainFunction, which special-cases
        // `DriverVirtual is nsf` to call `nsf1.Render(buffer, sampleCount/2, offset) * 2`
        // directly instead of mds.Update(), the same bypass this port already uses for SID
        // (see LoadSid). An earlier version of this method instead built a MDSound.nes_intf
        // instance and bound Update on it like VGM's NES block - that chip is never touched
        // by nsf.cs's 6502 execution at all, so it would have produced silence.
        // The cAPU/cDMC/cFDS/cMMC5/cN160/cVRC6/cVRC7/cFME7 back-references are still needed
        // even though none of them carry an Update delegate: nsf.cs's own Render() reads
        // cAPU.Volume/cDMC.Volume/etc directly (unconditionally for APU/DMC, and for whichever
        // expansion chips the file's soundchip byte enables) to scale each sub-chip's output,
        // so these MDSound.MDSound.Chip objects exist purely as Volume carriers.
        private static MusicEngineSession LoadNsf(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            nsf driver = new(setting) { setting = setting, song = 0 };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;

            static MDSound.MDSound.Chip MakeVolumeCarrier(int volume) => new() { Volume = volume };

            driver.cAPU = MakeVolumeCarrier(setting.balance.APUVolume);
            driver.cDMC = MakeVolumeCarrier(setting.balance.DMCVolume);
            driver.cFDS = MakeVolumeCarrier(setting.balance.FDSVolume);
            driver.cMMC5 = MakeVolumeCarrier(setting.balance.MMC5Volume);
            driver.cN160 = MakeVolumeCarrier(setting.balance.N160Volume);
            driver.cVRC6 = MakeVolumeCarrier(setting.balance.VRC6Volume);
            driver.cVRC7 = MakeVolumeCarrier(setting.balance.VRC7Volume);
            driver.cFME7 = MakeVolumeCarrier(setting.balance.FME7Volume);

            return new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = driver,
                SampleRate = sampleRate,
                Format = EnmFileFormat.NSF,
                ActiveChips = "NES APU+DMC+FDS+MMC5+N106+VRC6+VRC7+FME7 (NTSC, real 6502 CPU exec via MDSound.np)",
                RenderSamples = (b, off, count) => (int)driver.Render(b, (uint)(count / 2), off) * 2,
                // NSF writes its own PCM and owns per-voice volume carriers outside
                // MDSound's resampler list, so exposing generic per-chip faders here would
                // misleadingly draw controls that cannot affect its renderer.  Master gain
                // still applies to the completed PCM in MusicEngineSession.
                MasterVolume = setting.balance.MasterVolume,
                DefaultMasterVolume = setting.balance.MasterVolume,
            };
        }

        // Game Boy (GBS). Single fixed chip (the DMG APU, same MDSound.gb class VgmEngine.cs's
        // VGM DMG block already uses), at the real GB CPU/APU clock. gbs.cs owns its own
        // Sharp LR35902 CPU emulator (Driver/GBS/CPU.cs etc.) to actually execute the tune's
        // 6502-family code and drive the DMG chip's registers, but the chip itself still goes
        // through the normal MDSound.MDSound.Chip/ChipRegister path (unlike SID).
        private static MusicEngineSession LoadGbs(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            gbs driver = new(setting) { setting = setting, song = 0 };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;

            MDSound.gb dmg = new();
            var chip = new MDSound.MDSound.Chip
            {
                type = MDSound.MDSound.enmInstrumentType.DMG,
                ID = 0,
                Instrument = dmg,
                Update = dmg.Update,
                Start = dmg.Start,
                Stop = dmg.Stop,
                Reset = dmg.Reset,
                SamplingRate = sampleRate,
                Volume = setting.balance.DMGVolume,
                Clock = 4194304,
                Option = null,
            };
            driver.dmg = chip;

            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip> { chip };
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.GBS, "DMG=4194304Hz");
        }

        // PC Engine/TurboGrafx-16 (HES). Single fixed chip (HuC6280's built-in PSG, the same
        // MDSound.Ootake_PSG class VgmEngine.cs's VGM HuC6280 block already uses). hes.cs
        // (via m_hes.cs/km6280.cs) runs its own HuC6280 CPU emulator to execute the tune.
        // Clock matches Audio.cs's HesPlay exactly (3579545, not the PC Engine's ~7.16MHz
        // master clock - the original's own value, kept as-is rather than "corrected").
        private static MusicEngineSession LoadHes(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            hes driver = new() { setting = setting, song = 0 };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;

            MDSound.Ootake_PSG huc6280 = new();
            var chip = new MDSound.MDSound.Chip
            {
                type = MDSound.MDSound.enmInstrumentType.HuC6280,
                ID = 0,
                Instrument = huc6280,
                Update = huc6280.Update,
                Start = huc6280.Start,
                Stop = huc6280.Stop,
                Reset = huc6280.Reset,
                SamplingRate = sampleRate,
                Volume = setting.balance.HuC6280Volume,
                Clock = 3579545,
                Option = null,
            };
            driver.c6280 = chip;

            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip> { chip };
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.HES, "HuC6280=3579545Hz");
        }

        // S98 is architecturally the closest to VGM here: a per-file register-dump format
        // that declares its own chip list (S98.cs's S98Info.DeviceInfos, populated by
        // S98.init() from the file's device table) rather than a fixed one. DeviceType codes
        // are S98.cs's own (see its init() switch): 1=YM2149 (wired as AY8910, its closest
        // MDSound equivalent), 2=YM2203, 3=YM2612, 4=YM2608, 5=YM2151, 6=YM2413, 7=YM3526,
        // 8=YM3812, 9=YMF262, 15=AY8910, 16=SN76489 - the same chip set VgmEngine.cs's VGM
        // path already wires, just built dynamically here instead of from *ClockValue fields.
        private static MusicEngineSession LoadS98(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            S98 driver = new(setting) { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0))
                return null;
            if (driver.s98Info?.DeviceInfos == null) return null;

            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            var parts = new System.Collections.Generic.List<string>();
            void AddPart(string name, uint clock) => parts.Add($"{name}={clock}Hz");

            foreach (var dev in driver.s98Info.DeviceInfos)
            {
                MDSound.MDSound.Chip chip;
                switch (dev.DeviceType)
                {
                    case 1: case 15: // YM2149 / AY8910
                        {
                            MDSound.ay8910 ay = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.AY8910, ID = dev.ChipID, Instrument = ay, Update = ay.Update, Start = ay.Start, Stop = ay.Stop, Reset = ay.Reset, SamplingRate = sampleRate, Volume = setting.balance.AY8910Volume, Clock = dev.Clock, Option = null };
                            AddPart("AY8910", dev.Clock);
                            break;
                        }
                    case 2: // YM2203
                        {
                            MDSound.ym2203 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2203, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.YM2203Volume, Clock = dev.Clock, Option = null };
                            AddPart("YM2203", dev.Clock);
                            break;
                        }
                    case 3: // YM2612
                        {
                            MDSound.ym2612 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2612, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.YM2612Volume, Clock = dev.Clock, Option = null };
                            AddPart("YM2612", dev.Clock);
                            break;
                        }
                    case 4: // YM2608
                        {
                            MDSound.ym2608 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2608, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = 55467, Volume = setting.balance.YM2608Volume, Clock = dev.Clock, Option = new object[] { (Func<string, Stream>)Common.GetOPNARyhthmStream } };
                            AddPart("YM2608", dev.Clock);
                            break;
                        }
                    case 5: // YM2151
                        chip = MakeYM2151(setting, dev.Clock);
                        chip.ID = dev.ChipID;
                        AddPart("YM2151", dev.Clock);
                        break;
                    case 6: // YM2413
                        {
                            MDSound.emu2413 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM2413, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.YM2413Volume, Clock = dev.Clock, Option = null };
                            AddPart("YM2413", dev.Clock);
                            break;
                        }
                    case 7: // YM3526
                        {
                            MDSound.ym3526 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM3526, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.YM3526Volume, Clock = dev.Clock, Option = null };
                            AddPart("YM3526", dev.Clock);
                            break;
                        }
                    case 8: // YM3812
                        {
                            MDSound.ym3812 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YM3812, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.YM3812Volume, Clock = dev.Clock, Option = null };
                            AddPart("YM3812", dev.Clock);
                            break;
                        }
                    case 9: // YMF262
                        {
                            MDSound.ymf262 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.YMF262, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.YMF262Volume, Clock = dev.Clock, Option = null };
                            AddPart("YMF262", dev.Clock);
                            break;
                        }
                    case 16: // SN76489
                        {
                            MDSound.sn76489 c = new();
                            chip = new MDSound.MDSound.Chip { type = MDSound.MDSound.enmInstrumentType.SN76489, ID = dev.ChipID, Instrument = c, Update = c.Update, Start = c.Start, Stop = c.Stop, Reset = c.Reset, SamplingRate = sampleRate, Volume = setting.balance.SN76489Volume, Clock = dev.Clock, Option = null };
                            AddPart("SN76489", dev.Clock);
                            break;
                        }
                    default:
                        continue; // unrecognized/unsupported device type - skip, matching S98.cs's own init() (it just doesn't label unknown types)
                }
                lstChips.Add(chip);
            }

            string activeChips = parts.Count > 0 ? string.Join(" ", parts) : "(no supported chip)";
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.S98, activeChips);
        }

        // ZX Spectrum (AY). AY8910 (the Spectrum 128's/ZX-compatible AY-3-8910) plus ZXBeep
        // (the base 48K Spectrum's 1-bit beeper, which many AY tunes also drive) - matches
        // Audio.cs's AyPlay clocks (1789773/2, the Spectrum's AY/beeper clock halving).
        // Neither has its own Setting.balance.* slider (see header note on MND's mpcmpp for
        // the same situation) so this reuses AY8910Volume for both.
        //
        // IMPORTANT CAVEAT: AY files are Z80 machine code the original plays back through a
        // full Z80 CPU emulator (Konamiman.Z80dotNet, a real NuGet package - see
        // Driver/AY/AY.cs's `using Konamiman.Z80dotNet;`). This cloud sandbox has no network
        // access to nuget.org (the same limitation documented in macos/README.md for
        // MDPlayerUI's Avalonia packages), so Driver/AY/AY.cs and port.cs could NOT be
        // sandbox-build-verified this round - only syntax/structure-reviewed against the
        // original. They need a real `dotnet build` on the Mac (where the real package
        // restores normally) before this path can be trusted.
        private static MusicEngineSession LoadAy(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            Driver.AY.AY driver = new() { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.AY8910 }, 0, 0))
                return null;

            uint clock = 1789773 / 2;
            MDSound.ay8910 ay = new();
            MDSound.zxbeep beep = new();
            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.AY8910,
                    ID = 0,
                    Instrument = ay,
                    Update = ay.Update,
                    Start = ay.Start,
                    Stop = ay.Stop,
                    Reset = ay.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.AY8910Volume,
                    Clock = clock,
                    Option = null,
                },
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.ZXBeep,
                    ID = 0,
                    Instrument = beep,
                    Update = beep.Update,
                    Start = beep.Start,
                    Stop = beep.Stop,
                    Reset = beep.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.AY8910Volume, // no dedicated ZXBeep slider - see header note
                    Clock = clock,
                    Option = null,
                },
            };
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.AY, $"AY8910={clock}Hz ZXBeep={clock}Hz");
        }

        // ZGM is a niche format whose own upstream implementation (Driver/ZGM/ZgmChip/
        // ChipFactory.cs) only actually implements the YM2609 (OPNA-family) device type -
        // every other device ID in its switch statement returns null (dead/unimplemented
        // even in the original Windows build, not something this port narrowed down). So
        // wiring only YM2609 here matches upstream's real behavior, not a simplification of
        // it. Chip list is built the same dynamic way as S98: iterate the chips the driver's
        // own init() (via ChipFactory) already resolved, wire up whichever ones came back
        // non-null. SamplingRate=55467 and Volume=0 are Audio.cs's ZgmPlay's own hard-coded
        // values (not this port's choice) - kept as-is for fidelity.
        private static MusicEngineSession LoadZgm(byte[] buf, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);
            Driver.ZGM.zgm driver = new() { setting = setting };
            if (!driver.init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.YM2203 }, 0, 0))
                return null;
            if (driver.chips == null) return null;

            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>();
            MDSound.ym2609 ym2609 = null;
            foreach (var ch in driver.chips)
            {
                if (ch.Device != Driver.ZGM.EnmZGMDevice.YM2609) continue;
                ym2609 ??= new MDSound.ym2609();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2609,
                    ID = 0,
                    Instrument = ym2609,
                    Update = ym2609.Update,
                    Start = ym2609.Start,
                    Stop = ym2609.Stop,
                    Reset = ym2609.Reset,
                    SamplingRate = 55467,
                    Volume = setting.balance.YM2609Volume,
                    Clock = (uint)ch.defineInfo.clock,
                    Option = new object[] { (Func<string, Stream>)Common.GetOPNARyhthmStream },
                });
            }

            string activeChips = lstChips.Count > 0 ? "YM2609" : "(no supported chip)";
            return Finish(setting, chipRegister, mds, sampleRate, samplingBuffer, driver, lstChips,
                EnmFileFormat.ZGM, activeChips);
        }
    }
}
