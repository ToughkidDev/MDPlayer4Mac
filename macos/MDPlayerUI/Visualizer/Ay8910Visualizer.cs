// Port of MDPlayer/MDPlayerx64/form/KB/PSG/frmAY8910.cs - the AY8910 (PSG/SSG) channel
// visualizer: 3 tone/noise channel rows (LED volume bar, piano keyboard, tone/noise mode
// icon, tone-period hex readout, hardware-envelope-mode flag) plus the chip-wide hardware
// envelope generator's Frequency/Type readouts. No stereo pan, no operator table - AY8910
// is the simplest chip window ported so far (simpler even than SN76489, which at least has
// per-channel pan and a noise-mode text readout).
//
// Data source: reads chipRegister.psgRegisterAY8910 directly instead of through the
// original's unported Audio.GetAY8910Register wrapper (a thin pass-through to the same
// ChipRegister field), same approach as every other chip visualizer in this port.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ay8910Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.AY8910 newParam = new();
        private readonly MDChipParams.AY8910 oldParam = new();

        public PixelScreen Screen => screen;

        public Ay8910Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffAy8910.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeAY8910");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmAY8910.cs:75 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] ay8910Register = chipRegister.psgRegisterAY8910[ChipID];
            if (ay8910Register == null) return;

            for (int ch = 0; ch < 3; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];

                bool t = (ay8910Register[0x07] & (0x1 << ch)) == 0;
                bool n = (ay8910Register[0x07] & (0x8 << ch)) == 0;
                channel.tn = (t ? 1 : 0) + (n ? 2 : 0);
                newParam.nfrq = ay8910Register[0x06] & 0x1f;
                newParam.efrq = ay8910Register[0x0c] * 0x100 + ay8910Register[0x0b];
                newParam.etype = ay8910Register[0x0d] & 0xf;

                int v = ay8910Register[0x08 + ch] & 0x1f;
                v = v > 15 ? 15 : v;
                channel.volumeL = v;
                channel.volume = (int)((t || n ? 1 : 0) * v * (20.0 / 16.0));
                if (!t && !n && channel.volume > 0)
                {
                    channel.volume--;
                }

                int ft = ay8910Register[0x00 + ch * 2];
                int ct = ay8910Register[0x01 + ch * 2];
                int tp = (ct << 8) | ft;
                if (tp == 0) tp = 1;
                channel.freq = tp;

                if (channel.volume == 0)
                {
                    channel.note = -1;
                }
                else
                {
                    float clock = clockHz != 0 ? clockHz : 1789772.5f;
                    float ftone = clock / (8.0f * tp);
                    channel.note = Common.searchSSGNote(ftone);
                }

                channel.ex = (ay8910Register[0x08 + ch] & 0xf0) != 0;
            }
        }

        // frmAY8910.cs:142 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffAy8910.Volume(screen, 280, 8 + c * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffAy8910.KeyBoardDCSG(screen, 32, 8 + c * 8, ref oyc.note, nyc.note);
                DrawBuffAy8910.ToneNoise(screen, 6, 2, c, ref oyc.tn, nyc.tn, ref oyc.tntp, nyc.mask == true ? 1 : 0);

                DrawBuffAy8910.ChAy8910(screen, c, ref oyc.mask, nyc.mask);
                if (oyc.volumeL != nyc.volumeL)
                {
                    DrawBuffAy8910.DrawFont4(screen, 272, 8 + c * 8, nyc.volumeL.ToString("00"));
                    oyc.volumeL = nyc.volumeL;
                }
                DrawBuffAy8910.Font4Hex12Bit(screen, 256, 8 + c * 8, ref oyc.freq, nyc.freq);
                DrawBuffAy8910.DrawNesSw(screen, 268, 8 + c * 8, ref oyc.ex, nyc.ex);
            }

            DrawBuffAy8910.Nfrq(screen, 5, 8, ref oldParam.nfrq, newParam.nfrq);
            DrawBuffAy8910.Efrq(screen, 18, 8, ref oldParam.efrq, newParam.efrq);
            DrawBuffAy8910.Etype(screen, 33, 8, ref oldParam.etype, newParam.etype);

            screen.Present();
        }
    }
}
