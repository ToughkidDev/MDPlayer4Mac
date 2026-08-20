// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmOKIM6295.cs - the OKI MSM6295 ADPCM sample
// player visualizer (widely used in arcade boards, including NMK's bank-switching variant).
// 4 channels, one 8px row per channel, each showing a channel badge, a single-column volume
// LED bar (no L/R split - unlike most PCM-family chips, this one's output isn't panned), and
// 20-bit sample start/end address hex readouts. Below the 4 channel rows: a 32-bit master
// clock hex readout, a pin7 state hex readout, and 4 NMK112 bank-select hex readouts.
//
// Data source: reads chipRegister.GetOKIM6295Info(chipId) - already present in ChipRegister.cs
// but as `internal` (added in an earlier partial port pass, never actually reachable from
// MDPlayerUI since it's a separate assembly); widened to `public` alongside this file.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class OKIM6295Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.OKIM6295 newParam = new();
        private readonly MDChipParams.OKIM6295 oldParam = new();

        public PixelScreen Screen => screen;

        public OKIM6295Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmOKIM6295.cs's screenChangeParams never reads a clock value.

            DrawBuffOKIM6295.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeMSM6295");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmOKIM6295.cs:87 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.okim6295.okim6295Info info = chipRegister.GetOKIM6295Info(ChipID);
            if (info == null) return;

            for (int c = 0; c < 4; c++)
            {
                MDChipParams.Channel nyc = newParam.channels[c];

                if (info.keyon[c])
                {
                    nyc.volume = 19;
                }
                else
                {
                    nyc.volume -= (nyc.volume > 0) ? 1 : 0;
                }
                nyc.sadr = info.chInfo[c].stAdr;
                nyc.eadr = info.chInfo[c].edAdr;
            }

            newParam.masterClock = info.masterClock;
            newParam.pin7State = info.pin7State;
            newParam.nmkBank[0] = info.nmkBank[0];
            newParam.nmkBank[1] = info.nmkBank[1];
            newParam.nmkBank[2] = info.nmkBank[2];
            newParam.nmkBank[3] = info.nmkBank[3];
        }

        // frmOKIM6295.cs:117 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 4; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffOKIM6295.ChOKIM6295(screen, c, ref oyc.mask, nyc.mask);
                DrawBuffOKIM6295.Volume(screen, 64 * 4, c * 8 + 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffOKIM6295.Font4Hex20Bit(screen, 36, 8 + c * 8, ref oyc.sadr, nyc.sadr);
                DrawBuffOKIM6295.Font4Hex20Bit(screen, 60, 8 + c * 8, ref oyc.eadr, nyc.eadr);

                DrawBuffOKIM6295.Font4HexByte(screen, 36 + c * 16, 48, ref oldParam.nmkBank[c], newParam.nmkBank[c]);
            }

            DrawBuffOKIM6295.Font4Hex32Bit(screen, 24, 40, ref oldParam.masterClock, newParam.masterClock);
            DrawBuffOKIM6295.Font4HexByte(screen, 80, 40, ref oldParam.pin7State, newParam.pin7State);

            screen.Present();
        }
    }
}
