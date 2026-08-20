// Port of MDPlayer/MDPlayerx64/form/KB/WF/frmHuC6280.cs - the PC Engine/TurboGrafx-16
// built-in PSG channel visualizer. 6 wavetable channels laid out 3-per-row across 2 rows;
// channels 4-5 additionally carry a noise generator. Each channel shows a 32-sample
// waveform, byte-packed L/R pan, DDA (direct D/A playback) status, and separate L/R LED
// volume meters, plus shared main-volume and LFO control/frequency readouts.
//
// Data source: reads chipRegister.GetHuC6280Register(chipID) - MDSound.Ootake_PSG.
// huc6280_state, a new one-line forward added to ChipRegister.cs alongside this file (see
// that file's comment).
//
// Preserved quirk: `channel.inst = psg.wave` in screenChangeParams REPLACES the channel's
// waveform array reference outright (not an element-by-element copy) - it aliases whatever
// array the live emulation core's PSG.wave field currently points to, exactly as
// frmHuC6280.cs does. Not "fixed" into a copy, per this port's philosophy of preserving
// original behavior/quirks rather than correcting them.
//
// Deliberate simplification: `tp` hardcoded 0 (frmHuC6280.cs derives it from a "use real
// hardware" UI setting this port doesn't carry), same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in this
// port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Huc6280Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.HuC6280 newParam = new();
        private readonly MDChipParams.HuC6280 oldParam = new();

        public PixelScreen Screen => screen;

        public Huc6280Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmHuC6280.cs's screenChangeParams never reads a clock value.

            DrawBuffHuc6280.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeHuC6280");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmHuC6280.cs:89 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.Ootake_PSG.huc6280_state chip = chipRegister.GetHuC6280Register(ChipID);
            if (chip == null) return;

            for (int ch = 0; ch < 6; ch++)
            {
                MDSound.Ootake_PSG.PSG psg = chip.Psg[ch];
                if (psg == null) continue;

                MDChipParams.Channel channel = newParam.channels[ch];
                channel.volumeL = (int)(psg.volumeL >> 10);
                channel.volumeR = (int)(psg.volumeR >> 10);
                channel.volumeL = System.Math.Min(channel.volumeL, 19);
                channel.volumeR = System.Math.Min(channel.volumeR, 19);

                channel.pan = (int)((psg.volumeL & 0xf) | ((psg.volumeR & 0xf) << 4));

                channel.inst = psg.wave;

                channel.dda = psg.bDDA;

                int tp = (int)psg.frq;
                if (tp == 0) tp = 1;

                float ftone = 3579545.0f / 32.0f / tp;
                channel.note = Common.searchSSGNote(ftone);
                if (channel.volumeL == 0 && channel.volumeR == 0) channel.note = -1;

                if (ch < 4) continue;

                channel.noise = psg.bNoiseOn;
                channel.nfrq = (int)psg.noiseFrq;
            }

            newParam.mvolL = (int)chip.MainVolumeL;
            newParam.mvolR = (int)chip.MainVolumeR;
            newParam.LfoCtrl = (int)chip.LfoCtrl;
            newParam.LfoFrq = (int)chip.LfoFrq;
        }

        // frmHuC6280.cs:134 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 6; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffHuc6280.KeyBoard(screen, c, ref oyc.note, nyc.note);

                DrawBuffHuc6280.VolumeToHuC6280(screen, c, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffHuc6280.VolumeToHuC6280(screen, c, 2, ref oyc.volumeR, nyc.volumeR);
                DrawBuffHuc6280.PanType2(screen, c, ref oyc.pan, nyc.pan);

                DrawBuffHuc6280.WaveFormToHuC6280(screen, c, ref oyc.inst, nyc.inst);
                DrawBuffHuc6280.DDAToHuC6280(screen, c, ref oyc.dda, nyc.dda);

                DrawBuffHuc6280.ChHuc6280(screen, c, ref oyc.mask, nyc.mask);

                if (c < 4) continue;

                DrawBuffHuc6280.NoiseToHuC6280(screen, c, ref oyc.noise, nyc.noise);
                DrawBuffHuc6280.NoiseFrqToHuC6280(screen, c, ref oyc.nfrq, nyc.nfrq);
            }

            DrawBuffHuc6280.MainVolumeToHuC6280(screen, 0, ref oldParam.mvolL, newParam.mvolL);
            DrawBuffHuc6280.MainVolumeToHuC6280(screen, 1, ref oldParam.mvolR, newParam.mvolR);

            DrawBuffHuc6280.LfoCtrlToHuC6280(screen, ref oldParam.LfoCtrl, newParam.LfoCtrl);
            DrawBuffHuc6280.LfoFrqToHuC6280(screen, ref oldParam.LfoFrq, newParam.LfoFrq);

            screen.Present();
        }
    }
}
