// Shared "load a VGM file and wire it up to MDSound" setup, factored out of
// macos/EngineSmokeTest/Program.cs so both the file-rendering smoke test and any live
// playback front-end (see macos/LivePlayer/, macos/MDPlayerUI/) share one setup path
// instead of duplicating it.
//
// Chip wiring below is adapted from the original Windows Audio.Init's chip-dispatch block
// (MDPlayer/MDPlayerx64/Audio.cs, ~line 8760 onward) but deliberately simplified: the
// original picks between multiple emulator backends per chip (software emu vs. real
// hardware vs. alternate emu cores) via Setting.<Chip>Type[n].UseEmu[]/UseReal[], and
// supports dual-chip VGMs. This port always uses the single default software emulator and
// only wires up chip instance 0 - real-hardware output and dual-chip VGMs are out of scope
// (see AudioShim.cs's header comment for the same reasoning applied to file-format
// detection). Add more chips here (and to EnmChip.* below) following the same pattern as
// Audio.cs's dispatch block for any chip not yet covered.
using System.IO.Compression;

namespace MDPlayer
{
    // Renamed from VgmEngineSession: this is now the shared result type for ANY supported
    // music format (see MusicEngine.cs), not just VGM. `Driver` is deliberately typed as the
    // common `baseDriver` base class (Vgm/xgm/xgm2/sid/mndrv/ZMS/MXDRV/nsf/gbs/hes/S98/zgm/AY
    // all derive from it) rather than a concrete driver type, since callers (EngineSmokeTest/
    // LivePlayer/MDPlayerUI) only ever need baseDriver's shared surface (`Stopped`,
    // `oneFrameProc()`, `Version`) regardless of which format was actually loaded.
    public class MusicEngineSession
    {
        public Setting Setting;
        public ChipRegister ChipRegister;
        public MDSound.MDSound Mds;
        public baseDriver Driver;
        public uint SampleRate;
        public EnmFileFormat Format;

        // Human-readable "which chips does this file actually use" summary. For VGM/VGZ this
        // is computed dynamically from the Vgm instance's *ClockValue fields (see
        // VgmEngine.Load, the only format where the chip set varies per-file); every other
        // format uses a fixed chip set for its whole platform, so MusicEngine.cs just sets
        // this to a fixed string at load time. Stored rather than computed live so this class
        // doesn't need per-format knowledge of internal driver fields.
        public string ActiveChips = "(no supported chip)";
        public string DescribeActiveChips() => ActiveChips;

        // How a caller pulls rendered stereo samples out of this session. For every format
        // except SID this is just `mds.Update(buf, off, count, driver.oneFrameProc)` (set by
        // MusicEngine.Finish); SID substitutes a delegate that calls sid.Render(...) directly
        // instead, since it bypasses MDSound.MDSound.Update()/oneFrameProc entirely (see
        // MusicEngine.LoadSid's header comment).
        public System.Func<short[], int, int, int> RenderSamples;

        // Per-chip clock (Hz) actually used to wire up MDSound for this session, keyed by
        // chip type - e.g. ChipClocks[MDSound.MDSound.enmInstrumentType.SN76489] gives the
        // SN76489 clock a channel visualizer needs to turn a raw tone-register divisor into
        // a note/frequency (mirrors Audio.ClockSN76489 on the Windows side, which the
        // per-chip visualizer forms - e.g. frmSN76489.cs's SearchSSGNote - read directly).
        // Populated once per Load() call from the same Chip list MDSound.Init() consumes, so
        // it's always in sync with what's actually playing; absent entries (chip not present
        // in this file) simply aren't in the dictionary.
        public System.Collections.Generic.Dictionary<MDSound.MDSound.enmInstrumentType, uint> ChipClocks
            = new();

        internal static string DescribeVgmActiveChips(Vgm Vgm)
        {
            List<string> parts = new();
            void Add(string name, uint clock)
            {
                if (clock != 0) parts.Add($"{name}={clock}Hz");
            }

            Add("SN76489", Vgm.SN76489ClockValue);
            Add("YM2612", Vgm.YM2612ClockValue);
            Add("YM2151", Vgm.YM2151ClockValue);
            Add("YM2203", Vgm.YM2203ClockValue);
            Add("YM2608", Vgm.YM2608ClockValue);
            Add("YM2610", Vgm.YM2610ClockValue);
            Add("YM3812", Vgm.YM3812ClockValue);
            Add("YM3526", Vgm.YM3526ClockValue);
            Add("YMF262", Vgm.YMF262ClockValue);
            Add("AY8910", Vgm.AY8910ClockValue);
            Add("YM2413", Vgm.YM2413ClockValue);
            Add("K051649", Vgm.K051649ClockValue);
            Add("SEGAPCM", Vgm.SEGAPCMClockValue);
            Add("RF5C68", Vgm.RF5C68ClockValue);
            Add("RF5C164", Vgm.RF5C164ClockValue);
            Add("PWM", Vgm.PWMClockValue);
            Add("C140", Vgm.C140ClockValue);
            Add("OKIM6258", Vgm.OKIM6258ClockValue);
            Add("OKIM6295", Vgm.OKIM6295ClockValue);
            Add("Y8950", Vgm.Y8950ClockValue);
            Add("YMF278B", Vgm.YMF278BClockValue & 0x7fffffff);
            Add("YMF271", Vgm.YMF271ClockValue & 0x7fffffff);
            Add("YMZ280B", Vgm.YMZ280BClockValue & 0x7fffffff);
            Add("DMG", Vgm.DMGClockValue);
            Add("NES", Vgm.NESClockValue);
            Add("MultiPCM", Vgm.MultiPCMClockValue);
            Add("uPD7759", Vgm.uPD7759ClockValue);
            Add("K054539", Vgm.K054539ClockValue);
            Add("HuC6280", Vgm.HuC6280ClockValue & 0x7fffffff);
            Add("K053260", Vgm.K053260ClockValue);
            Add("POKEY", Vgm.POKEYClockValue & 0x3fffffff);
            Add("QSound", Vgm.QSoundClockValue);
            Add("WSwan", Vgm.WSwanClockValue & 0x3fffffff);
            Add("SAA1099", Vgm.SAA1099ClockValue & 0x3fffffff);
            Add("ES5503", Vgm.ES5503ClockValue & 0x3fffffff);
            Add("X1_010", Vgm.X1_010ClockValue & 0x3fffffff);
            Add("C352", Vgm.C352ClockValue & 0x7fffffff);
            Add("GA20", Vgm.GA20ClockValue & 0x7fffffff);

            return parts.Count > 0 ? string.Join(" ", parts) : "(no supported chip)";
        }
    }

    public static class VgmEngine
    {
        // .vgz files (the format most real-world VGM downloads, e.g. from vgmrips.net,
        // actually come in) are just gzip-compressed .vgm files - detected here via the
        // gzip magic bytes (0x1f 0x8b) rather than the file extension, so a renamed/
        // mislabeled file still works. Mirrors the original Windows Audio.cs's
        // Common.unzipFile, which does the same decompression via
        // System.IO.Compression.GZipStream (a portable BCL type, no Windows dependency,
        // so this needed no porting work beyond copying the approach).
        private static byte[] DecompressIfGzip(byte[] buf)
        {
            if (buf.Length < 2 || buf[0] != 0x1f || buf[1] != 0x8b)
                return buf;

            using MemoryStream input = new(buf);
            using GZipStream gzip = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        // samplingBuffer is MDSound's internal resample buffer size (in frames), not the
        // caller's per-Update() chunk size - unrelated to how many samples you pull per call.
        public static MusicEngineSession Load(byte[] vgmBuf, uint samplingBuffer = 2048)
        {
            vgmBuf = DecompressIfGzip(vgmBuf);

            Setting setting = Setting.Load();
            setting.ApplyChipTypeDefaults();

            uint sampleRate = (uint)setting.outputDevice.SampleRate;

            MDSound.MDSound mds = new(sampleRate, samplingBuffer, null);

            ChipRegister chipRegister = new(
                setting
                , null // pianoRollMng - not exercised outside the PianoRoll UI
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
            EnmChip[] useChip = new EnmChip[]
            {
                EnmChip.SN76489, EnmChip.YM2612,
                EnmChip.YM2151, EnmChip.YM2203, EnmChip.YM2608, EnmChip.YM2610,
                EnmChip.YM3812, EnmChip.YM3526, EnmChip.YMF262,
                EnmChip.AY8910, EnmChip.YM2413, EnmChip.K051649, EnmChip.SEGAPCM,
                EnmChip.RF5C68, EnmChip.RF5C164, EnmChip.PWM, EnmChip.C140,
                EnmChip.OKIM6258, EnmChip.OKIM6295, EnmChip.Y8950, EnmChip.YMF278B,
                EnmChip.YMF271, EnmChip.YMZ280B, EnmChip.DMG,
                EnmChip.NES, EnmChip.DMC, EnmChip.FDS,
                EnmChip.MultiPCM, EnmChip.uPD7759, EnmChip.K054539, EnmChip.HuC6280,
                EnmChip.K053260, EnmChip.POKEY, EnmChip.QSound, EnmChip.WSwan,
                EnmChip.SAA1099, EnmChip.ES5503, EnmChip.X1_010, EnmChip.C352,
                EnmChip.GA20,
            };
            if (!vgm.init(vgmBuf, chipRegister, EnmModel.VirtualModel, useChip, latency, waitTime))
            {
                return null;
            }

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

            if (vgm.YM2151ClockValue != 0)
            {
                MDSound.ym2151 ym2151 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2151,
                    ID = 0,
                    Instrument = ym2151,
                    Update = ym2151.Update,
                    Start = ym2151.Start,
                    Stop = ym2151.Stop,
                    Reset = ym2151.Reset,
                    SamplingRate = vgm.YM2151ClockValue / 64, // matches Audio.cs: OPM's internal rate is Clock/64
                    Volume = setting.balance.YM2151Volume,
                    Clock = vgm.YM2151ClockValue,
                    Option = null,
                });
            }

            if (vgm.YM2203ClockValue != 0)
            {
                MDSound.ym2203 ym2203 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2203,
                    ID = 0,
                    Instrument = ym2203,
                    Update = ym2203.Update,
                    Start = ym2203.Start,
                    Stop = ym2203.Stop,
                    Reset = ym2203.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YM2203Volume,
                    Clock = vgm.YM2203ClockValue,
                    Option = null,
                });
            }

            if (vgm.YM2608ClockValue != 0)
            {
                MDSound.ym2608 ym2608 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2608,
                    ID = 0,
                    Instrument = ym2608,
                    Update = ym2608.Update,
                    Start = ym2608.Start,
                    Stop = ym2608.Stop,
                    Reset = ym2608.Reset,
                    SamplingRate = 55467, // matches Audio.cs - OPNA's rhythm mixer runs at a fixed rate
                    Volume = setting.balance.YM2608Volume,
                    Clock = vgm.YM2608ClockValue,
                    // Rhythm ADPCM sample loader - gracefully returns null (silent rhythm
                    // channel) if the sample file isn't found on disk, so this is safe even
                    // without the original PC-98/OPNA rhythm sample files present.
                    Option = new object[] { (Func<string, System.IO.Stream>)Common.GetOPNARyhthmStream },
                });
            }

            if (vgm.YM2610ClockValue != 0)
            {
                MDSound.ym2610 ym2610 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2610,
                    ID = 0,
                    Instrument = ym2610,
                    Update = ym2610.Update,
                    Start = ym2610.Start,
                    Stop = ym2610.Stop,
                    Reset = ym2610.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YM2610Volume,
                    Clock = vgm.YM2610ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.YM3812ClockValue != 0)
            {
                MDSound.ym3812 ym3812 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM3812,
                    ID = 0,
                    Instrument = ym3812,
                    Update = ym3812.Update,
                    Start = ym3812.Start,
                    Stop = ym3812.Stop,
                    Reset = ym3812.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YM3812Volume,
                    Clock = vgm.YM3812ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.YM3526ClockValue != 0)
            {
                MDSound.ym3526 ym3526 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM3526,
                    ID = 0,
                    Instrument = ym3526,
                    Update = ym3526.Update,
                    Start = ym3526.Start,
                    Stop = ym3526.Stop,
                    Reset = ym3526.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YM3526Volume,
                    Clock = vgm.YM3526ClockValue,
                    Option = null,
                });
            }

            if (vgm.YMF262ClockValue != 0)
            {
                MDSound.ymf262 ymf262 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YMF262,
                    ID = 0,
                    Instrument = ymf262,
                    Update = ymf262.Update,
                    Start = ymf262.Start,
                    Stop = ymf262.Stop,
                    Reset = ymf262.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YMF262Volume,
                    Clock = vgm.YMF262ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.AY8910ClockValue != 0)
            {
                MDSound.ay8910 ay8910 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.AY8910,
                    ID = 0,
                    Instrument = ay8910,
                    Update = ay8910.Update,
                    Start = ay8910.Start,
                    Stop = ay8910.Stop,
                    Reset = ay8910.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.AY8910Volume,
                    Clock = (vgm.AY8910ClockValue & 0x7fffffff) / 2, // matches Audio.cs
                    Option = null,
                });
            }

            if (vgm.YM2413ClockValue != 0)
            {
                // Always the default software OPLL emu - the original also supports a VRC7
                // variant (Setting.YM2413VRC7Flag), out of scope here (see class header).
                MDSound.emu2413 opll = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YM2413emu,
                    ID = 0,
                    Instrument = opll,
                    Update = opll.Update,
                    Start = opll.Start,
                    Stop = opll.Stop,
                    Reset = opll.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YM2413Volume,
                    Clock = vgm.YM2413ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.K051649ClockValue != 0)
            {
                MDSound.K051649 k051649 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.K051649,
                    ID = 0,
                    Instrument = k051649,
                    Update = k051649.Update,
                    Start = k051649.Start,
                    Stop = k051649.Stop,
                    Reset = k051649.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.K051649Volume,
                    Clock = vgm.K051649ClockValue,
                    Option = null,
                });
            }

            if (vgm.SEGAPCMClockValue != 0)
            {
                MDSound.segapcm segapcm = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.SEGAPCM,
                    ID = 0,
                    Instrument = segapcm,
                    Update = segapcm.Update,
                    Start = segapcm.Start,
                    Stop = segapcm.Stop,
                    Reset = segapcm.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.SEGAPCMVolume,
                    Clock = vgm.SEGAPCMClockValue,
                    Option = new object[] { vgm.SEGAPCMInterface },
                });
            }

            if (vgm.RF5C68ClockValue != 0)
            {
                MDSound.rf5c68 rf5c68 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.RF5C68,
                    ID = 0,
                    Instrument = rf5c68,
                    Update = rf5c68.Update,
                    Start = rf5c68.Start,
                    Stop = rf5c68.Stop,
                    Reset = rf5c68.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.RF5C68Volume,
                    Clock = vgm.RF5C68ClockValue,
                    Option = null,
                });
            }

            if (vgm.RF5C164ClockValue != 0)
            {
                // RF5C164 (Sega CD PCM) shares its emulator core with RF5C68 - MDSound
                // exposes it as the "scd_pcm" class rather than a second rf5c68-named type.
                MDSound.scd_pcm rf5c164 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.RF5C164,
                    ID = 0,
                    Instrument = rf5c164,
                    Update = rf5c164.Update,
                    Start = rf5c164.Start,
                    Stop = rf5c164.Stop,
                    Reset = rf5c164.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.RF5C164Volume,
                    Clock = vgm.RF5C164ClockValue,
                    Option = null,
                });
            }

            if (vgm.PWMClockValue != 0)
            {
                MDSound.pwm pwm = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.PWM,
                    ID = 0,
                    Instrument = pwm,
                    Update = pwm.Update,
                    Start = pwm.Start,
                    Stop = pwm.Stop,
                    Reset = pwm.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.PWMVolume,
                    Clock = vgm.PWMClockValue,
                    Option = null,
                });
            }

            if (vgm.C140ClockValue != 0)
            {
                MDSound.c140 c140 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.C140,
                    ID = 0,
                    Instrument = c140,
                    Update = c140.Update,
                    Start = c140.Start,
                    Stop = c140.Stop,
                    Reset = c140.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.C140Volume,
                    Clock = vgm.C140ClockValue,
                    Option = new object[] { vgm.C140Type }, // PCM interleave type (ASIC219 vs. System21 etc.)
                });
            }

            if (vgm.OKIM6258ClockValue != 0)
            {
                MDSound.okim6258 okim6258 = new();
                MDSound.MDSound.Chip okim6258Chip = new()
                {
                    type = MDSound.MDSound.enmInstrumentType.OKIM6258,
                    ID = 0,
                    Instrument = okim6258,
                    Update = okim6258.Update,
                    Start = okim6258.Start,
                    Stop = okim6258.Stop,
                    Reset = okim6258.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.OKIM6258Volume,
                    Clock = vgm.OKIM6258ClockValue,
                    Option = new object[] { (int)vgm.OKIM6258Type },
                };
                // OKIM6258 can change its own output rate at runtime (VGM chip-specific
                // commands) - this callback keeps MDSound's resampler in sync when that
                // happens. See ChangeChipSampleRate below.
                okim6258.okim6258_set_srchg_cb(0, (chip, newRate) => ChangeChipSampleRate(chip, newRate, sampleRate), okim6258Chip);
                lstChips.Add(okim6258Chip);
            }

            if (vgm.OKIM6295ClockValue != 0)
            {
                MDSound.okim6295 okim6295 = new();
                MDSound.MDSound.Chip okim6295Chip = new()
                {
                    type = MDSound.MDSound.enmInstrumentType.OKIM6295,
                    ID = 0,
                    Instrument = okim6295,
                    Update = okim6295.Update,
                    Start = okim6295.Start,
                    Stop = okim6295.Stop,
                    Reset = okim6295.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.OKIM6295Volume,
                    Clock = vgm.OKIM6295ClockValue,
                    Option = null,
                };
                okim6295.okim6295_set_srchg_cb(0, (chip, newRate) => ChangeChipSampleRate(chip, newRate, sampleRate), okim6295Chip);
                lstChips.Add(okim6295Chip);
            }

            if (vgm.Y8950ClockValue != 0)
            {
                MDSound.y8950 y8950 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.Y8950,
                    ID = 0,
                    Instrument = y8950,
                    Update = y8950.Update,
                    Start = y8950.Start,
                    Stop = y8950.Stop,
                    Reset = y8950.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.Y8950Volume,
                    Clock = vgm.Y8950ClockValue,
                    Option = null,
                });
            }

            if (vgm.YMF278BClockValue != 0)
            {
                MDSound.ymf278b ymf278b = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YMF278B,
                    ID = 0,
                    Instrument = ymf278b,
                    Update = ymf278b.Update,
                    Start = ymf278b.Start,
                    Stop = ymf278b.Stop,
                    Reset = ymf278b.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YMF278BVolume,
                    Clock = vgm.YMF278BClockValue & 0x7fffffff,
                    // Looks for a yrw801.rom sample ROM next to the app; gracefully plays
                    // without wavetable samples (FM part still works) if it's not found -
                    // see ymf278b.cs's ymf278b_load_rom, which File.Exists-guards this.
                    Option = new object[] { Common.GetApplicationFolder() },
                });
            }

            if (vgm.YMF271ClockValue != 0)
            {
                MDSound.ymf271 ymf271 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YMF271,
                    ID = 0,
                    Instrument = ymf271,
                    Update = ymf271.Update,
                    Start = ymf271.Start,
                    Stop = ymf271.Stop,
                    Reset = ymf271.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YMF271Volume,
                    Clock = vgm.YMF271ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.YMZ280BClockValue != 0)
            {
                MDSound.ymz280b ymz280b = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.YMZ280B,
                    ID = 0,
                    Instrument = ymz280b,
                    Update = ymz280b.Update,
                    Start = ymz280b.Start,
                    Stop = ymz280b.Stop,
                    Reset = ymz280b.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.YMZ280BVolume,
                    Clock = vgm.YMZ280BClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.DMGClockValue != 0)
            {
                // "DMG" = the Game Boy's built-in APU (Dot Matrix Game).
                MDSound.gb dmg = new();
                lstChips.Add(new MDSound.MDSound.Chip
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
                    Clock = vgm.DMGClockValue,
                    Option = null,
                });
            }

            if (vgm.NESClockValue != 0)
            {
                // The NES's APU (pulse/triangle/noise) plus its DMC and FDS sub-units all
                // share one nes_intf instance - matches Audio.cs exactly: only the "Nes"
                // registration gets an Update delegate (that's what actually pulls audio
                // each frame), DMC/FDS are registered so ChipRegister can route their
                // specific VGM write commands to the same instance, not for a second
                // independent Update call.
                MDSound.nes_intf nes = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.Nes,
                    ID = 0,
                    Instrument = nes,
                    Update = nes.Update,
                    Start = nes.Start,
                    Stop = nes.Stop,
                    Reset = nes.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.APUVolume,
                    Clock = vgm.NESClockValue,
                    Option = null,
                });
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.DMC,
                    ID = 0,
                    Instrument = nes,
                    Start = nes.Start,
                    Stop = nes.Stop,
                    Reset = nes.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.DMCVolume,
                    Clock = vgm.NESClockValue,
                    Option = null,
                });
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.FDS,
                    ID = 0,
                    Instrument = nes,
                    Start = nes.Start,
                    Stop = nes.Stop,
                    Reset = nes.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.FDSVolume,
                    Clock = vgm.NESClockValue,
                    Option = null,
                });
            }

            if (vgm.MultiPCMClockValue != 0)
            {
                MDSound.multipcm multipcm = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.MultiPCM,
                    ID = 0,
                    Instrument = multipcm,
                    Update = multipcm.Update,
                    Start = multipcm.Start,
                    Stop = multipcm.Stop,
                    Reset = multipcm.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.MultiPCMVolume,
                    Clock = vgm.MultiPCMClockValue,
                    Option = null,
                });
            }

            if (vgm.uPD7759ClockValue != 0)
            {
                MDSound.upd7759 upd7759 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.uPD7759,
                    ID = 0,
                    Instrument = upd7759,
                    Update = upd7759.Update,
                    Start = upd7759.Start,
                    Stop = upd7759.Stop,
                    Reset = upd7759.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.uPD7759Volume,
                    Clock = vgm.uPD7759ClockValue,
                    Option = null,
                });
            }

            if (vgm.K054539ClockValue != 0)
            {
                MDSound.K054539 k054539 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.K054539,
                    ID = 0,
                    Instrument = k054539,
                    Update = k054539.Update,
                    Start = k054539.Start,
                    Stop = k054539.Stop,
                    Reset = k054539.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.K054539Volume,
                    Clock = vgm.K054539ClockValue,
                    Option = null,
                });
            }

            if (vgm.HuC6280ClockValue != 0)
            {
                // MDSound's HuC6280 (PC Engine) PSG emulator is ported from the Ootake
                // emulator, hence the class name.
                MDSound.Ootake_PSG huc6280 = new();
                lstChips.Add(new MDSound.MDSound.Chip
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
                    Clock = vgm.HuC6280ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (vgm.K053260ClockValue != 0)
            {
                MDSound.K053260 k053260 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.K053260,
                    ID = 0,
                    Instrument = k053260,
                    Update = k053260.Update,
                    Start = k053260.Start,
                    Stop = k053260.Stop,
                    Reset = k053260.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.K053260Volume,
                    Clock = vgm.K053260ClockValue,
                    Option = null,
                });
            }

            if (vgm.POKEYClockValue != 0)
            {
                MDSound.pokey pokey = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.POKEY,
                    ID = 0,
                    Instrument = pokey,
                    Update = pokey.Update,
                    Start = pokey.Start,
                    Stop = pokey.Stop,
                    Reset = pokey.Reset,
                    // matches Audio.cs: POKEY runs its own sampling rate off its clock,
                    // not the output device rate.
                    SamplingRate = vgm.POKEYClockValue & 0x3fffffff,
                    Volume = setting.balance.POKEYVolume,
                    Clock = vgm.POKEYClockValue & 0x3fffffff,
                    Option = null,
                });
            }

            if (vgm.QSoundClockValue != 0)
            {
                MDSound.Qsound_ctr qsound = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.QSoundCtr,
                    ID = 0,
                    Instrument = qsound,
                    Update = qsound.Update,
                    Start = qsound.Start,
                    Stop = qsound.Stop,
                    Reset = qsound.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.QSoundVolume,
                    Clock = vgm.QSoundClockValue,
                    Option = null,
                });
            }

            if (vgm.WSwanClockValue != 0)
            {
                MDSound.ws_audio wswan = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.WSwan,
                    ID = 0,
                    Instrument = wswan,
                    Update = wswan.Update,
                    Start = wswan.Start,
                    Stop = wswan.Stop,
                    Reset = wswan.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.WSwanVolume,
                    Clock = vgm.WSwanClockValue & 0x3fffffff,
                    Option = null,
                });
            }

            if (vgm.SAA1099ClockValue != 0)
            {
                MDSound.saa1099 saa1099 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.SAA1099,
                    ID = 0,
                    Instrument = saa1099,
                    Update = saa1099.Update,
                    Start = saa1099.Start,
                    Stop = saa1099.Stop,
                    Reset = saa1099.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.SAA1099Volume,
                    Clock = vgm.SAA1099ClockValue & 0x3fffffff,
                    Option = null,
                });
            }

            if (vgm.ES5503ClockValue != 0)
            {
                MDSound.Es5503 es5503 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.ES5503,
                    ID = 0,
                    Instrument = es5503,
                    Update = es5503.Update,
                    Start = es5503.Start,
                    Stop = es5503.Stop,
                    Reset = es5503.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.ES5503Volume,
                    Clock = vgm.ES5503ClockValue & 0x3fffffff,
                    Option = new object[] { (byte)vgm.ES5503Ch },
                });
            }

            if (vgm.X1_010ClockValue != 0)
            {
                MDSound.x1_010 x1_010 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.X1_010,
                    ID = 0,
                    Instrument = x1_010,
                    Update = x1_010.Update,
                    Start = x1_010.Start,
                    Stop = x1_010.Stop,
                    Reset = x1_010.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.X1_010Volume,
                    Clock = vgm.X1_010ClockValue & 0x3fffffff,
                    Option = null,
                });
            }

            if (vgm.C352ClockValue != 0)
            {
                MDSound.c352 c352 = new();
                int divider = vgm.C352ClockDivider != 0 ? vgm.C352ClockDivider : 288;
                c352.c352_set_options((byte)(vgm.C352ClockValue >> 31));
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.C352,
                    ID = 0,
                    Instrument = c352,
                    Update = c352.Update,
                    Start = c352.Start,
                    Stop = c352.Stop,
                    Reset = c352.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.C352Volume,
                    Clock = (vgm.C352ClockValue & 0x7fffffff) / (uint)divider,
                    Option = new object[] { vgm.C352ClockDivider },
                });
            }

            if (vgm.GA20ClockValue != 0)
            {
                MDSound.iremga20 ga20 = new();
                lstChips.Add(new MDSound.MDSound.Chip
                {
                    type = MDSound.MDSound.enmInstrumentType.GA20,
                    ID = 0,
                    Instrument = ga20,
                    Update = ga20.Update,
                    Start = ga20.Start,
                    Stop = ga20.Stop,
                    Reset = ga20.Reset,
                    SamplingRate = sampleRate,
                    Volume = setting.balance.GA20Volume,
                    Clock = vgm.GA20ClockValue & 0x7fffffff,
                    Option = null,
                });
            }

            if (lstChips.Count == 0)
            {
                return null;
            }

            chipRegister.initChipRegister(lstChips.ToArray());
            mds.Init(sampleRate, samplingBuffer, lstChips.ToArray());

            System.Collections.Generic.Dictionary<MDSound.MDSound.enmInstrumentType, uint> chipClocks = new();
            foreach (MDSound.MDSound.Chip c in lstChips) chipClocks[c.type] = c.Clock;

            return new MusicEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Driver = vgm,
                SampleRate = sampleRate,
                Format = EnmFileFormat.VGM,
                ActiveChips = MusicEngineSession.DescribeVgmActiveChips(vgm),
                RenderSamples = (b, off, count) => mds.Update(b, off, count, vgm.oneFrameProc),
                ChipClocks = chipClocks,
            };
        }

        // OKIM6258/OKIM6295 can change their own output sample rate at runtime via
        // VGM chip-specific stream commands; MDSound's resampler needs to be told when
        // that happens so it keeps pulling the right number of source samples per output
        // sample. Adapted from Audio.cs's static ChangeChipSampleRate - the original reads
        // the device rate from a global Setting.outputDevice.SampleRate; this port takes
        // deviceSampleRate as a parameter instead, since Setting is a per-session instance
        // here rather than a single global (see MusicEngineSession).
        private static void ChangeChipSampleRate(MDSound.MDSound.Chip chip, int newSmplRate, uint deviceSampleRate)
        {
            if (chip.SamplingRate == newSmplRate)
                return;

            chip.SamplingRate = (uint)newSmplRate;
            if (chip.SamplingRate < deviceSampleRate)
                chip.Resampler = 0x01;
            else if (chip.SamplingRate == deviceSampleRate)
                chip.Resampler = 0x02;
            else
                chip.Resampler = 0x03;
            chip.SmpP = 1;
            chip.SmpNext -= chip.SmpLast;
            chip.SmpLast = 0x00;
        }
    }
}
