// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmDMG.cs - the DMG (original Game Boy) sound
// channel visualizer. 4 channels: 2 pulse/square (channel 0 also has frequency sweep), 1
// custom-wavetable channel (32 4-bit samples, drawn as a live bar-graph strip), and 1 noise
// channel - each with its own hand-placed layout, plus a per-channel stereo pan readout
// (DMG's stereo panning predates every other NES-family chip ported so far in this port).
//
// Data source: reads chipRegister.GetDMGRegister(chipID) - MDSound.gb.gb_sound_t, a new
// one-line forward added to ChipRegister.cs alongside this file (see that file's comment).
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop (blank piano keys) is skipped, same as every
// other chip in this port - the first real ScreenDrawParams call naturally draws everything
// via the normal dirty-diff path.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class DmgVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.DMG newParam = new();
        private readonly MDChipParams.DMG oldParam = new();

        public PixelScreen Screen => screen;

        public DmgVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmDMG.cs's screenChangeParams never reads a clock value.

            DrawBuffDmg.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeDMG");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmDMG.cs:319 searchSSGNote - DMG's OWN private note-search, genuinely different
        // from Common.searchSSGNote: divides freq by 4 before comparing (`1 << (6 - 4)`),
        // searches 12*9 entries instead of 12*8, and stops at the first local minimum
        // (`else break`) instead of scanning the whole table for the global minimum.
        // Preserved verbatim rather than reusing Common.searchSSGNote.
        private static int SearchSsgNote(float freq)
        {
            float m = float.MaxValue;
            int n = 0;
            for (int i = 0; i < 12 * 9; i++)
            {
                float a = Math.Abs(freq / (1 << (6 - 4)) - Tables.freqTbl[i]);
                if (m > a)
                {
                    m = a;
                    n = i;
                }
                else break;
            }
            return n;
        }

        // frmDMG.cs:86 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.gb.gb_sound_t dat = chipRegister.GetDMGRegister(ChipID);
            if (dat == null) return;

            newParam.channels[0].pan = dat.snd_control.mode1_left * 2 + dat.snd_control.mode1_right;
            newParam.channels[1].pan = dat.snd_control.mode2_left * 2 + dat.snd_control.mode2_right;
            newParam.channels[2].pan = dat.snd_control.mode3_left * 2 + dat.snd_control.mode3_right;
            newParam.channels[3].pan = dat.snd_control.mode4_left * 2 + dat.snd_control.mode4_right;

            newParam.channels[0].freq = dat.snd_1.frequency;
            newParam.channels[1].freq = dat.snd_2.frequency;
            newParam.channels[2].freq = dat.snd_3.frequency;
            newParam.channels[3].freq = dat.snd_4.reg[3] & 0x7; // pfq
            newParam.channels[3].bit[47] = (dat.snd_4.reg[3] & 0x8) != 0; // poly
            newParam.channels[3].srcFreq = (dat.snd_4.reg[3] & 0xf0) >> 4; // pc

            newParam.channels[0].bit[0] = dat.snd_1.length_enabled;
            newParam.channels[1].bit[0] = dat.snd_2.length_enabled;
            newParam.channels[2].bit[0] = dat.snd_3.length_enabled;
            newParam.channels[3].bit[0] = dat.snd_4.length_enabled;

            newParam.channels[0].bit[1] = (dat.snd_1.reg[4] & 0x80) != 0;
            newParam.channels[1].bit[1] = (dat.snd_2.reg[4] & 0x80) != 0;
            newParam.channels[2].bit[1] = (dat.snd_3.reg[4] & 0x80) != 0;
            newParam.channels[3].bit[1] = (dat.snd_4.reg[4] & 0x80) != 0;

            newParam.channels[0].bit[2] = dat.snd_1.envelope_direction == 1;
            newParam.channels[1].bit[2] = dat.snd_2.envelope_direction == 1;
            // channel 2 (wavetable) has no envelope direction.
            newParam.channels[3].bit[2] = dat.snd_4.envelope_direction == 1;

            newParam.channels[0].bit[3] = dat.snd_1.sweep_direction == -1;

            newParam.channels[0].inst[0] = dat.snd_1.envelope_time;
            newParam.channels[1].inst[0] = dat.snd_2.envelope_time;
            newParam.channels[3].inst[0] = dat.snd_4.envelope_time;

            newParam.channels[0].inst[1] = dat.snd_1.envelope_value;
            newParam.channels[1].inst[1] = dat.snd_2.envelope_value;
            newParam.channels[3].inst[1] = dat.snd_4.envelope_value;

            newParam.channels[0].inst[2] = dat.snd_1.length;
            newParam.channels[1].inst[2] = dat.snd_2.length;
            newParam.channels[3].inst[2] = dat.snd_4.length;

            newParam.channels[0].inst[3] = dat.snd_1.duty;
            newParam.channels[1].inst[3] = dat.snd_2.duty;

            newParam.channels[0].inst[4] = dat.snd_1.sweep_time;
            newParam.channels[0].inst[5] = dat.snd_1.sweep_shift;

            newParam.channels[2].inst[4] = dat.snd_3.length;
            newParam.channels[2].inst[5] = dat.snd_3.level;

            for (int i = 0; i < 16; i++)
            {
                newParam.wf[i * 2] = (byte)((dat.snd_regs[0x20 + i] >> 4) & 0xf);
                newParam.wf[i * 2 + 1] = (byte)(dat.snd_regs[0x20 + i] & 0xf);
            }

            const int r = 10;
            newParam.channels[0].volumeL = Math.Min(dat.snd_1.envelope_value * dat.snd_control.mode1_left * 16 / r, 19);
            newParam.channels[0].volumeR = Math.Min(dat.snd_1.envelope_value * dat.snd_control.mode1_right * 16 / r, 19);
            newParam.channels[1].volumeL = Math.Min(dat.snd_2.envelope_value * dat.snd_control.mode2_left * 16 / r, 19);
            newParam.channels[1].volumeR = Math.Min(dat.snd_2.envelope_value * dat.snd_control.mode2_right * 16 / r, 19);
            int lvl = dat.snd_3.level == 0 ? 0 : 19 >> (dat.snd_3.level - 1);
            newParam.channels[2].volumeL = Math.Min(lvl * dat.snd_control.mode3_left * 19 / r, 19);
            newParam.channels[2].volumeR = Math.Min(lvl * dat.snd_control.mode3_right * 19 / r, 19);
            newParam.channels[3].volumeL = Math.Min(dat.snd_4.envelope_value * dat.snd_control.mode4_left * 16 / r, 19);
            newParam.channels[3].volumeR = Math.Min(dat.snd_4.envelope_value * dat.snd_control.mode4_right * 16 / r, 19);

            for (int i = 0; i < 3; i++)
            {
                newParam.channels[i].note = -1;
                if (newParam.channels[i].volumeL != 0 || newParam.channels[i].volumeR != 0)
                {
                    float ftone = 4194304.0f / (4 * 2 * (2048.0f - newParam.channels[i].freq));
                    newParam.channels[i].note = Math.Max(Math.Min(SearchSsgNote(ftone), 8 * 12), 0);
                }
            }
        }

        // frmDMG.cs:191 screenDrawParams.
        public void ScreenDrawParams()
        {
            MDChipParams.Channel oyc = oldParam.channels[0];
            MDChipParams.Channel nyc = newParam.channels[0];
            DrawBuffDmg.Pan(screen, 24, 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
            DrawBuffDmg.Font4Hex12Bit(screen, 260, 8, ref oyc.freq, nyc.freq);
            DrawBuffDmg.VolumeXY(screen, 68, 2, 1, ref oyc.volumeL, nyc.volumeL);
            DrawBuffDmg.VolumeXY(screen, 68, 3, 1, ref oyc.volumeR, nyc.volumeR);
            DrawBuffDmg.DrawNesSw(screen, 60, 40, ref oyc.bit[0], nyc.bit[0]); // CC
            DrawBuffDmg.DrawNesSw(screen, 60, 48, ref oyc.bit[1], nyc.bit[1]); // Ini
            DrawBuffDmg.DrawNesSw(screen, 28, 64, ref oyc.bit[2], nyc.bit[2]); // Env.Dir
            DrawBuffDmg.DrawNesSw(screen, 88, 56, ref oyc.bit[3], nyc.bit[3]); // Sweep Dec
            DrawBuffDmg.Font4Int1(screen, 28, 48, ref oyc.inst[0], nyc.inst[0]); // Env. Spd
            DrawBuffDmg.Font4Int2(screen, 24, 56, ref oyc.inst[1], nyc.inst[1]); // Env. Vol
            DrawBuffDmg.Font4Int2(screen, 56, 64, ref oyc.inst[2], nyc.inst[2]); // Len
            DrawBuffDmg.Font4Int1(screen, 60, 56, ref oyc.inst[3], nyc.inst[3]); // Duty
            DrawBuffDmg.Font4Int1(screen, 88, 48, ref oyc.inst[4], nyc.inst[4]); // Sweep time
            DrawBuffDmg.Font4Int1(screen, 88, 64, ref oyc.inst[5], nyc.inst[5]); // Sweep shift
            DrawBuffDmg.KeyBoardDMG(screen, 0, ref oyc.note, nyc.note);
            DrawBuffDmg.ChDmg(screen, 0, ref oyc.mask, nyc.mask);

            oyc = oldParam.channels[1];
            nyc = newParam.channels[1];
            DrawBuffDmg.Pan(screen, 24, 16, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
            DrawBuffDmg.Font4Hex12Bit(screen, 260, 16, ref oyc.freq, nyc.freq);
            DrawBuffDmg.VolumeXY(screen, 68, 4, 1, ref oyc.volumeL, nyc.volumeL);
            DrawBuffDmg.VolumeXY(screen, 68, 5, 1, ref oyc.volumeR, nyc.volumeR);
            DrawBuffDmg.DrawNesSw(screen, 152, 40, ref oyc.bit[0], nyc.bit[0]); // CC
            DrawBuffDmg.DrawNesSw(screen, 152, 48, ref oyc.bit[1], nyc.bit[1]); // Ini
            DrawBuffDmg.DrawNesSw(screen, 120, 64, ref oyc.bit[2], nyc.bit[2]); // Env.Dir
            DrawBuffDmg.Font4Int1(screen, 120, 48, ref oyc.inst[0], nyc.inst[0]); // Env. Spd
            DrawBuffDmg.Font4Int2(screen, 116, 56, ref oyc.inst[1], nyc.inst[1]); // Env. Vol
            DrawBuffDmg.Font4Int2(screen, 148, 64, ref oyc.inst[2], nyc.inst[2]); // Len
            DrawBuffDmg.Font4Int1(screen, 152, 56, ref oyc.inst[3], nyc.inst[3]); // Duty
            DrawBuffDmg.KeyBoardDMG(screen, 1, ref oyc.note, nyc.note);
            DrawBuffDmg.ChDmg(screen, 1, ref oyc.mask, nyc.mask);

            oyc = oldParam.channels[2];
            nyc = newParam.channels[2];
            DrawBuffDmg.Pan(screen, 24, 24, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
            DrawBuffDmg.Font4Hex12Bit(screen, 260, 24, ref oyc.freq, nyc.freq);
            DrawBuffDmg.VolumeXY(screen, 68, 6, 1, ref oyc.volumeL, nyc.volumeL);
            DrawBuffDmg.VolumeXY(screen, 68, 7, 1, ref oyc.volumeR, nyc.volumeR);
            DrawBuffDmg.DrawNesSw(screen, 228, 40, ref oyc.bit[0], nyc.bit[0]); // CC
            DrawBuffDmg.DrawNesSw(screen, 228, 48, ref oyc.bit[1], nyc.bit[1]); // Ini
            // Channel 2 has no Env.Dir readout.
            DrawBuffDmg.Font4Int3(screen, 220, 56, ref oyc.inst[4], nyc.inst[4]); // Len
            DrawBuffDmg.Font4Int1(screen, 228, 64, ref oyc.inst[5], nyc.inst[5]); // Vol
            DrawBuffDmg.KeyBoardDMG(screen, 2, ref oyc.note, nyc.note);
            DrawBuffDmg.ChDmg(screen, 2, ref oyc.mask, nyc.mask);

            oyc = oldParam.channels[3];
            nyc = newParam.channels[3];
            DrawBuffDmg.Pan(screen, 24, 32, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
            DrawBuffDmg.VolumeXY(screen, 68, 8, 1, ref oyc.volumeL, nyc.volumeL);
            DrawBuffDmg.VolumeXY(screen, 68, 9, 1, ref oyc.volumeR, nyc.volumeR);
            DrawBuffDmg.Font4Int1(screen, 316, 40, ref oyc.freq, nyc.freq);
            DrawBuffDmg.DrawNesSw(screen, 316, 48, ref oyc.bit[47], nyc.bit[47]);
            DrawBuffDmg.Font4Int2(screen, 312, 56, ref oyc.srcFreq, nyc.srcFreq);
            DrawBuffDmg.DrawNesSw(screen, 288, 40, ref oyc.bit[0], nyc.bit[0]); // CC
            DrawBuffDmg.DrawNesSw(screen, 288, 48, ref oyc.bit[1], nyc.bit[1]); // Ini
            DrawBuffDmg.DrawNesSw(screen, 260, 64, ref oyc.bit[2], nyc.bit[2]); // Env.Dir
            DrawBuffDmg.Font4Int1(screen, 260, 48, ref oyc.inst[0], nyc.inst[0]); // Env. Spd
            DrawBuffDmg.Font4Int2(screen, 256, 56, ref oyc.inst[1], nyc.inst[1]); // Env. Vol
            DrawBuffDmg.Font4Int2(screen, 284, 64, ref oyc.inst[2], nyc.inst[2]); // Len
            DrawBuffDmg.ChDmg(screen, 3, ref oyc.mask, nyc.mask);

            DrawBuffDmg.WaveFormToDMG(screen, 168, 58, ref oldParam.wf, newParam.wf);

            screen.Present();
        }
    }
}
