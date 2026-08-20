// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmOKIM6258.cs - the OKI MSM6258 ADPCM voice
// synthesis chip visualizer. Unlike every multi-channel PCM-family chip ported so far, this
// chip is single-channel (mono ADPCM voice output split to L/R via a 2-bit pan register) - so
// MDChipParams.OKIM6258 holds direct fields rather than a Channel[] array. One 8px row shows a
// raw single-tile pan icon, three 5-digit zero-padded decimal readouts (master clock frequency
// in kHz, clock divider, and the derived playback frequency in kHz), independent L/R volume
// LED bars, and a static (no channel-number) badge icon.
//
// Data source: reads chipRegister.GetOKIM6258Register(chipId) - a new one-line forward added
// to ChipRegister.cs, forwarding to mds.ReadOKIM6258Status (mirroring Audio.GetOKIM6258Register
// - this port's ChipRegister keeps mds private, same reason a wrapper was needed for
// GetK053260Register/GetRf5c164Register before it).
//
// No clock constructor parameter is read - the original's screenChangeParams derives both the
// displayed master/divider/playback frequencies AND the volume entirely from the live register
// state (master_clock/divider/data_in/status/pan), never from an Audio.ClockXxx property.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class OKIM6258Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.OKIM6258 newParam = new();
        private readonly MDChipParams.OKIM6258 oldParam = new();

        public PixelScreen Screen => screen;

        public OKIM6258Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmOKIM6258.cs's screenChangeParams never reads a clock value.

            DrawBuffOKIM6258.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeMSM6258");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();

            newParam.pan = 3;
        }

        // frmOKIM6258.cs:86 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.okim6258.okim6258_state okim6258State = chipRegister.GetOKIM6258Register(ChipID);
            if (okim6258State == null) return;

            switch (okim6258State.pan & 0x3)
            {
                case 0:
                case 3:
                    newParam.pan = 3;
                    break;
                case 1:
                    newParam.pan = 2;
                    break;
                case 2:
                    newParam.pan = 1;
                    break;
            }

            newParam.masterFreq = (int)(okim6258State.master_clock / 1000);
            newParam.divider = (int)okim6258State.divider;
            if (okim6258State.divider == 0) newParam.pbFreq = 0;
            else newParam.pbFreq = (int)(okim6258State.master_clock / okim6258State.divider / 1000);

            int v = (int)((System.Math.Abs(okim6258State.data_in - 128) * 2 >> 3) * 1.2);
            if ((okim6258State.status & 0x2) == 0) v = 0;
            v = System.Math.Min(v, 38);
            if (newParam.volumeL < v && ((newParam.pan & 0x2) != 0))
            {
                newParam.volumeL = v;
            }
            else
            {
                newParam.volumeL--;
            }
            if (newParam.volumeR < v && ((newParam.pan & 0x1) != 0))
            {
                newParam.volumeR = v;
            }
            else
            {
                newParam.volumeR--;
            }
        }

        // frmOKIM6258.cs:131 screenDrawParams.
        public void ScreenDrawParams()
        {
            MDChipParams.OKIM6258 ost = oldParam;
            MDChipParams.OKIM6258 nst = newParam;

            DrawBuffOKIM6258.PanToOKIM6258(screen, ref ost.pan, nst.pan, ref ost.pantp, 0);

            if (ost.masterFreq != nst.masterFreq)
            {
                DrawBuffOKIM6258.DrawFont4(screen, 12 * 4, 8, 0, nst.masterFreq.ToString("D5"));
                ost.masterFreq = nst.masterFreq;
            }

            if (ost.divider != nst.divider)
            {
                DrawBuffOKIM6258.DrawFont4(screen, 19 * 4, 8, 0, nst.divider.ToString("D5"));
                ost.divider = nst.divider;
            }

            if (ost.pbFreq != nst.pbFreq)
            {
                DrawBuffOKIM6258.DrawFont4(screen, 26 * 4, 8, 0, nst.pbFreq.ToString("D5"));
                ost.pbFreq = nst.pbFreq;
            }

            DrawBuffOKIM6258.Volume(screen, 256, 8, 1, ref ost.volumeL, nst.volumeL / 2);
            DrawBuffOKIM6258.Volume(screen, 256, 8, 2, ref ost.volumeR, nst.volumeR / 2);

            DrawBuffOKIM6258.ChOKIM6258(screen, ref ost.mask, nst.mask);

            screen.Present();
        }
    }
}
