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
                ".zms" => EnmFileFormat.ZMS,
                ".zmd" => EnmFileFormat.ZMD,
                ".mdx" => EnmFileFormat.MDX,
                ".mdr" => EnmFileFormat.MDR,
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
                EnmFileFormat.ZMS => LoadZms(buf, EnmFileFormat.ZMS, samplingBuffer),
                EnmFileFormat.ZMD => LoadZms(buf, EnmFileFormat.ZMD, samplingBuffer),
                EnmFileFormat.MDX => LoadMdx(buf, EnmFileFormat.MDX, samplingBuffer),
                EnmFileFormat.MDR => LoadMdx(buf, EnmFileFormat.MDR, samplingBuffer),
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
        // itself and hands back finished PCM through its own Render(), not through
        // MDSound.MDSound.Chip.Update()/ChipRegister's register-write dispatch - matching
        // Audio.cs's TrdVgmVirtualMainFunction, which special-cases `DriverVirtual is
        // Driver.MXDRV.MXDRV` to call `mXDRV.Render(buffer, offset+i, 2)` two samples (one
        // stereo frame) at a time rather than mds.Update(). The MDSound.MDSound.Chip entry
        // below (Update/Start/Stop/Reset all null) exists only so ChipRegister/MDSound.Init
        // have a non-empty chip list to register - matching Audio.cs's MdxPlay, which adds
        // the exact same null-delegate placeholder chip for its `mdxPCM_V` instrument.
        // Audio.cs also supports an alternate PCM8PP-chip-based path when
        // `setting.mxdrv.pcm8type != 0`, but its own comment above the default branch says
        // "mxdrvは特殊で必ずPCM8が必要" (MXDRV is special, it always needs [the built-in] PCM8) -
        // pcm8type 0 (mdxPCM's own built-in PCM8, no separate PCM8PP chip) is upstream's own
        // default, so using it here isn't a simplification this port introduced.
        // MDR is the same driver/chip set under a slightly different file variant.
        private static MusicEngineSession LoadMdx(byte[] buf, EnmFileFormat format, uint samplingBuffer)
        {
            var (setting, chipRegister, mds, sampleRate) = NewCommon(samplingBuffer);

            MDSound.ym2151_x68sound mdxPCM = new();
            mdxPCM.x68sound[0] = new MDSound.NX68Sound.X68Sound();
            mdxPCM.sound_Iocs[0] = new MDSound.NX68Sound.sound_iocs(mdxPCM.x68sound[0]);

            var lstChips = new System.Collections.Generic.List<MDSound.MDSound.Chip>
            {
                new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2151x68soundPCM,
                    ID = 0,
                    Instrument = mdxPCM,
                    Update = null,
                    Start = null,
                    Stop = null,
                    Reset = null,
                    Volume = setting.balance.YM2151Volume,
                    Clock = 4000000,
                },
            };

            Driver.MXDRV.MXDRV driver = new() { setting = setting, ExtendFile = null, pcm8type = 0 };
            if (!driver.Init(buf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.Unuse }, 0, 0, mdxPCM, null))
                return null;

            chipRegister.initChipRegister(lstChips.ToArray());
            mds.Init(sampleRate, samplingBuffer, lstChips.ToArray());

            return new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = driver,
                SampleRate = sampleRate,
                Format = format,
                ActiveChips = "YM2151+PCM8 (X68000 IOCS sound driver, via MXDRV/NX68Sound)",
                RenderSamples = (b, off, count) =>
                {
                    // Called 2 samples (1 stereo frame) at a time, matching Audio.cs's own
                    // MXDRV loop exactly - MXDRV.Render()'s internal OneFrameProc2 timer
                    // callback expects to be driven at this granularity for correct playback
                    // speed; handing it a large count in one call was not how the original
                    // ever exercised this path.
                    int total = 0;
                    for (int i = 0; i < count; i += 2)
                    {
                        int n = System.Math.Min(2, count - i);
                        int r = driver.Render(b, off + i, n);
                        if (r <= 0) break;
                        total += n;
                    }
                    return total;
                },
            };
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
                    Volume = 0,
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
