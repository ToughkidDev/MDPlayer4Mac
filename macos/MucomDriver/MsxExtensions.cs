using System;
using System.Collections.Generic;

namespace mucomDotNET.Driver
{
    public partial class Driver
    {
        // Called after Init and before MusicSTART. The five sequencing groups stay intact;
        // sound sinks, versioned SSG commands and declared raw timing follow Work.
        public void SetMsxTimerB(int value)
        {
            if(value is <0 or >255)throw new ArgumentOutOfRangeException(nameof(value));
            work.timerOPNA1.WriteReg(0x26,(byte)value);
        }
        public void ConfigureMsx(byte capabilities, Action<int, byte[], int> uploadWave, bool rawTiming = false, Action<int, int, int> sccRegister = null)
        {
            music2.MsxProfileConfigured = true;
            if(rawTiming)work.timerOPNA1 = new MsxRawTimer(renderingFreq, opnaMasterClock);
            music2.MsxNativeGroup = (capabilities & 1) != 0;
            music2.MsxSsg = (capabilities & 2) != 0;
            music2.MsxDedicatedScc = (capabilities & 4) != 0;
            music2.MsxSccRegister = sccRegister;
            music2.MsxWave = uploadWave;
            work.isDotNET = true;
            work.SSGExtend = false; // SCC+ approximation, not fmvgen virtual SSG registers.
        }
    }

    public partial class Music2
    {
        public bool MsxProfileConfigured { get; internal set; }
        public bool MsxNativeGroup { get; internal set; }
        public bool MsxSsg { get; internal set; }
        public bool MsxDedicatedScc { get; internal set; }
        public Action<int, int, int> MsxSccRegister { get; internal set; }
        private bool IsDedicatedScc => MsxDedicatedScc && work.soundWork.currentChip == 2 && work.soundWork.currentCh / 10 < 5;
        public Action<int, byte[], int> MsxWave { get; internal set; }
        private sealed class MsxWaveState
        {
            public byte[] Wave = MsxPreset(0);
            public int Gain = 255;
        }
        private readonly Dictionary<PGDAT, MsxWaveState> msxWaves = new Dictionary<PGDAT, MsxWaveState>();
        private static readonly int[] msxPanGains = { 0, 6, 13, 64, 115, 153, 192, 223, 255 };

        public static byte[] MsxPreset(int preset)
        {
            if (preset < 0 || preset > 9) throw new FormatException("Invalid SCC+ waveform preset.");
            byte[] wave = new byte[32];
            for (int n = 0; n < 32; n++)
            {
                int phase = n / 2;
                int value = preset < 8 ? (phase < 8 - preset ? -128 : 127)
                    : preset == 8 ? (phase < 8 ? phase - 4 : 11 - phase) * 32
                    : (phase < 7 ? phase : phase - 16) * 14;
                wave[n] = unchecked((byte)value);
            }
            return wave;
        }
        private byte MsxRead()
        {
            if (work.hl >= work.pg.mData.Length) throw new FormatException("Truncated SCC+ SSG command.");
            return (byte)work.pg.mData[work.hl++].dat;
        }
        private MsxWaveState MsxState()
        {
            if (!msxWaves.TryGetValue(work.pg, out MsxWaveState state))
                msxWaves.Add(work.pg, state = new MsxWaveState());
            return state;
        }
        private void RestoreMsxSsg()
        {
            if (!IsDedicatedScc && (!MsxSsg || work.soundWork.currentChip != 0 || work.soundWork.SSGF1 == 0)) return;
            MsxWaveState state = MsxState();
            MsxWave?.Invoke(IsDedicatedScc ? work.soundWork.currentCh / 10 : work.pg.channelNumber / 2, state.Wave, state.Gain);
        }
        private bool TryMsxSsgCommand(byte command)
        {
            if (IsDedicatedScc)
            {
                if (command == 0xed)
                {
                    if (MsxRead() != 255) throw new FormatException("SCC5 requires an inline 32-sample waveform.");
                    var state = MsxState();
                    state.Wave = new byte[32];
                    for (int i = 0; i < 32; i++) state.Wave[i] = MsxRead();
                    if (work.pg.pageNo == work.cd.currentPageNo) RestoreMsxSsg();
                    return true;
                }
                if (command == 0xf7)
                {
                    int chip=MsxRead(),port=MsxRead(),reg=MsxRead(),value=MsxRead();
                    if(chip!=0 || port!=0 || reg!=0x26)throw new FormatException("SCC5 permits only a primary Timer-B control write.");
                    PSGOUT(0,0,0x26,(byte)value);return true;
                }
                if (command is 0xee or 0xf0 or 0xf1 or 0xf2 or 0xf6 or 0xf8 or 0xfb)
                    throw new FormatException("Unsupported SCC5 hardware envelope, wave preset, register or pan command.");
                return false;
            }
            if (MsxProfileConfigured && !MsxSsg && (command is 0xed or 0xee))
                throw new FormatException("SCC+ command requires msPb0101 capability bit 1.");
            if (!MsxSsg) return false;
            bool ssg = work.soundWork.currentChip == 0 && work.soundWork.SSGF1 != 0;
            if (!ssg && (command is 0xf6 or 0xfb) && work.soundWork.SSGF1 != 0)
                throw new FormatException("Extended SSG wave/pan requires OPNA D/E/F.");
            if (command == 0xed || command == 0xee)
            {
                if (!ssg) throw new FormatException("SCC+ SSG command outside OPNA D/E/F.");
                MsxWaveState state = MsxState();
                byte argument = MsxRead();
                if (command == 0xee) state.Gain = argument;
                else if (argument == 255)
                {
                    state.Wave = new byte[32];
                    for (int i = 0; i < 32; i++) state.Wave[i] = MsxRead();
                }
                else state.Wave = MsxPreset(argument);
            }
            else if (ssg && command == 0xf6)
            {
                // The in-memory .MUC compiler uses upstream @I/@W encoding. Disk msPb
                // uses ED instead; both reach the same SCC+ state without stream rewriting.
                byte argument = MsxRead();
                MsxWaveState state = MsxState();
                if (argument == 255)
                {
                    int id = MsxRead();
                    if (work.ssgVoiceAtMusData == null || !work.ssgVoiceAtMusData.TryGetValue(id, out byte[] samples)
                        || samples.Length != 64) throw new FormatException("Missing 64-sample SSG waveform.");
                    state.Wave = new byte[32];
                    for (int i = 0; i < 32; i++)
                        state.Wave[i] = unchecked((byte)(((sbyte)samples[i*2] + (sbyte)samples[i*2+1]) >> 1));
                }
                else state.Wave = MsxPreset(argument);
            }
            else if (ssg && command == 0xfb)
            {
                int pan = MsxRead();
                if (pan > 80) throw new FormatException("Invalid SSG pan gain.");
                MsxState().Gain = (msxPanGains[pan / 9] + msxPanGains[pan % 9]) / 2;
            }
            else if (ssg && command == 0xf0)
            {
                int pan = MsxRead();
                if (pan > 6) throw new FormatException("Invalid SSG pan mode.");
                if (pan >= 4) MsxRead();
                MsxState().Gain = pan == 0 ? 0 : pan == 3 ? 255 : 128;
            }
            else return false;
            if (work.pg.pageNo == work.cd.currentPageNo) RestoreMsxSsg();
            return true;
        }
    }
}
