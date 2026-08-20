// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmFDS.cs - the Famicom Disk System's expansion
// audio channel visualizer. FDS adds one wavetable-synthesis channel to the NES APU: a
// 32-sample carrier waveform, modulated in pitch by a second 32-sample modulation waveform
// running through its own ramp envelope, plus a separate volume ramp envelope - both
// waveforms are drawn as a graphical bar-graph column (reusing the same rWavGraph sprite
// sheet as YM2609's PSG wavetable display) alongside numeric readouts of both envelopes'
// speed/gain/halt state and the shared master envelope clock.
//
// Data source: reads chipRegister.GetFDSRegister(chipID) - MDSound.np.np_nes_fds.NES_FDS,
// added to ChipRegister.cs alongside this file (see that file's comment), mirroring the
// NSF-direct/VGM-fallback shape added for NESDMC's GetAPURegister/GetDMCRegister.
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop (blank piano key, unmask badge) is skipped,
// same as every other chip in this port - the first real ScreenDrawParams call naturally
// draws everything via the normal dirty-diff path.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class FdsVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.FDS newParam = new();
        private readonly MDChipParams.FDS oldParam = new();

        public PixelScreen Screen => screen;

        // frmFDS.cs:89-91.
        private const double Log2_440 = 8.7813597135246596040696824762152;
        private const double Log_2 = 0.69314718055994530941723212145818;
        private const int Note440Hz = 12 * 4 + 9;

        public FdsVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmFDS.cs's screenChangeParams never reads a clock value.

            DrawBuffFds.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeFDS");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmFDS.cs:87 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.np.np_nes_fds.NES_FDS reg = chipRegister.GetFDSRegister(ChipID);
            if (reg == null) return;

            int freq = (int)reg.last_freq;
            int vol = (int)reg.last_vol;
            int note = -15 + (int)(12 * (Math.Log(freq) / Log_2 - Log2_440) + Note440Hz + 0.5);
            note = note < 0 ? -1 : (note > 120 ? -1 : note);
            note = vol == 0 ? -1 : note;
            vol = note == -1 ? 0 : vol;
            newParam.channel.note = note;
            newParam.channel.volume = Math.Min((int)(vol * 0.5), 19);

            for (int i = 0; i < 32; i++)
            {
                newParam.wave[i] = (reg.wave[1][i * 2 + 0] + reg.wave[1][i * 2 + 1]) >> 2;
                newParam.mod[i] = (reg.wave[0][i * 2 + 0] + reg.wave[0][i * 2 + 1]) << 1;
            }

            newParam.VolDir = reg.env_mode[1];
            newParam.VolSpd = (int)reg.env_speed[1];
            newParam.VolGain = (int)reg.env_out[1];
            newParam.VolDi = reg.env_halt;
            newParam.VolFrq = (int)reg.freq[1];
            newParam.VolHlR = reg.wav_halt;

            newParam.ModDir = reg.env_mode[0];
            newParam.ModSpd = (int)reg.env_speed[0];
            newParam.ModGain = (int)reg.env_out[0];
            newParam.ModDi = reg.mod_halt;
            newParam.ModFrq = (int)reg.freq[0];
            newParam.ModCnt = (int)reg.mod_pos;

            newParam.EnvSpd = (int)reg.master_env_speed;
            newParam.EnvVolSw = !reg.env_disable[1];
            newParam.EnvModSw = !reg.env_disable[0];

            newParam.MasterVol = reg.master_vol;
            newParam.WE = reg.wav_write;
        }

        // frmFDS.cs:138 screenDrawParams.
        public void ScreenDrawParams()
        {
            DrawBuffFds.KeyBoard(screen, 0, ref oldParam.channel.note, newParam.channel.note);
            DrawBuffFds.Volume(screen, 256, 8 + 0 * 8, 0, ref oldParam.channel.volume, newParam.channel.volume);

            DrawBuffFds.WaveFormToFDS(screen, 0, ref oldParam.wave, newParam.wave);
            DrawBuffFds.WaveFormToFDS(screen, 1, ref oldParam.mod, newParam.mod);

            DrawBuffFds.DrawNesSw(screen, 20 * 4, 6 * 4, ref oldParam.VolDir, newParam.VolDir);
            DrawBuffFds.Font4Int2(screen, 19 * 4, 8 * 4, ref oldParam.VolSpd, newParam.VolSpd);
            DrawBuffFds.Font4Int2(screen, 19 * 4, 10 * 4, ref oldParam.VolGain, newParam.VolGain);
            DrawBuffFds.DrawNesSw(screen, 20 * 4, 12 * 4, ref oldParam.VolDi, newParam.VolDi);
            DrawBuffFds.Font4Hex12Bit(screen, 26 * 4, 6 * 4, ref oldParam.VolFrq, newParam.VolFrq);
            DrawBuffFds.DrawNesSw(screen, 28 * 4, 8 * 4, ref oldParam.VolHlR, newParam.VolHlR);

            DrawBuffFds.DrawNesSw(screen, 48 * 4, 6 * 4, ref oldParam.ModDir, newParam.ModDir);
            DrawBuffFds.Font4Int2(screen, 47 * 4, 8 * 4, ref oldParam.ModSpd, newParam.ModSpd);
            DrawBuffFds.Font4Int2(screen, 47 * 4, 10 * 4, ref oldParam.ModGain, newParam.ModGain);
            DrawBuffFds.DrawNesSw(screen, 48 * 4, 12 * 4, ref oldParam.ModDi, newParam.ModDi);
            DrawBuffFds.Font4Hex12Bit(screen, 54 * 4, 6 * 4, ref oldParam.ModFrq, newParam.ModFrq);
            DrawBuffFds.Font4Int3(screen, 54 * 4, 8 * 4, ref oldParam.ModCnt, newParam.ModCnt);

            DrawBuffFds.Font4Int3(screen, 65 * 4, 6 * 4, ref oldParam.EnvSpd, newParam.EnvSpd);
            DrawBuffFds.DrawNesSw(screen, 67 * 4, 8 * 4, ref oldParam.EnvVolSw, newParam.EnvVolSw);
            DrawBuffFds.DrawNesSw(screen, 67 * 4, 10 * 4, ref oldParam.EnvModSw, newParam.EnvModSw);

            DrawBuffFds.Font4Int2(screen, 76 * 4, 6 * 4, ref oldParam.MasterVol, newParam.MasterVol);
            DrawBuffFds.DrawNesSw(screen, 77 * 4, 8 * 4, ref oldParam.WE, newParam.WE);

            DrawBuffFds.ChFds(screen, ref oldParam.channel.mask, newParam.channel.mask);

            screen.Present();
        }
    }
}
