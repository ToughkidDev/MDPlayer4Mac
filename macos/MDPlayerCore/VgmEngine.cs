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
    public class VgmEngineSession
    {
        public Setting Setting;
        public ChipRegister ChipRegister;
        public MDSound.MDSound Mds;
        public Vgm Vgm;
        public uint SampleRate;

        // Human-readable "which chips does this file actually use" summary, built from
        // whichever of Vgm's *ClockValue fields came back nonzero after Vgm.init(). Shared
        // by EngineSmokeTest/LivePlayer/MDPlayerUI so all three report the same chip list
        // instead of each hard-coding "SN76489/YM2612" (stale now that VgmEngine wires up
        // more chips than just those two).
        public string DescribeActiveChips()
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
        public static VgmEngineSession Load(byte[] vgmBuf, uint samplingBuffer = 2048)
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

            if (lstChips.Count == 0)
            {
                return null;
            }

            chipRegister.initChipRegister(lstChips.ToArray());
            mds.Init(sampleRate, samplingBuffer, lstChips.ToArray());

            return new VgmEngineSession
            {
                Setting = setting,
                ChipRegister = chipRegister,
                Mds = mds,
                Vgm = vgm,
                SampleRate = sampleRate,
            };
        }
    }
}
