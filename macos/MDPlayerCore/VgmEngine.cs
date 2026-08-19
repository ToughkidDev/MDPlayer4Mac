// Shared "load a VGM file and wire it up to MDSound" setup, factored out of
// macos/EngineSmokeTest/Program.cs so both the file-rendering smoke test and any live
// playback front-end (see macos/LivePlayer/) share one setup path instead of duplicating
// it. See EngineSmokeTest/Program.cs's header comment for why this deliberately wires up
// only SN76489 + YM2612 rather than porting Audio.VgmPlay's full ~20-chip dispatch.
namespace MDPlayer
{
    public class VgmEngineSession
    {
        public Setting Setting;
        public ChipRegister ChipRegister;
        public MDSound.MDSound Mds;
        public Vgm Vgm;
        public uint SampleRate;
    }

    public static class VgmEngine
    {
        // samplingBuffer is MDSound's internal resample buffer size (in frames), not the
        // caller's per-Update() chunk size - unrelated to how many samples you pull per call.
        public static VgmEngineSession Load(byte[] vgmBuf, uint samplingBuffer = 2048)
        {
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
            if (!vgm.init(vgmBuf, chipRegister, EnmModel.VirtualModel, new EnmChip[] { EnmChip.SN76489, EnmChip.YM2612 }, latency, waitTime))
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
