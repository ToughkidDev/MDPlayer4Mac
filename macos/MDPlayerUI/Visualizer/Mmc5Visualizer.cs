// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmMMC5.cs - the MMC5 cartridge mapper's
// expansion audio channel visualizer. 3 channels: 2 pulse/square (indices 0-1, numbered
// badges) and 1 PCM sample-playback channel (unnumbered wide badge) - each with its own
// hand-placed fixed screen position, same bespoke-layout style as NESDMC/FDS.
//
// Data source: reads chipRegister.GetMMC5Register(chipID) - a composed 10-byte register
// snapshot, added to ChipRegister.cs alongside this file (see that file's comment).
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in this
// port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Mmc5Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.MMC5 newParam = new();
        private readonly MDChipParams.MMC5 oldParam = new();

        public PixelScreen Screen => screen;

        // frmMMC5.cs:90-92.
        private const double Log2_440 = 8.7813597135246596040696824762152;
        private const double Log_2 = 0.69314718055994530941723212145818;
        private const int Note440Hz = 12 * 4 + 9;

        public Mmc5Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmMMC5.cs's screenChangeParams never reads a clock value.

            DrawBuffMmc5.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeMMC5");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmMMC5.cs:88 screenChangeParams.
        public void ScreenChangeParams()
        {
            byte[] reg = chipRegister.GetMMC5Register(ChipID);
            if (reg == null) return;

            for (int i = 0; i < 2; i++)
            {
                MDChipParams.Channel channel = newParam.sqrChannels[i];
                int freq = (reg[3 + i * 4] & 0x07) * 0x100 + reg[2 + i * 4];
                int vol = reg[i * 4] & 0xf;
                int note = 104 - (int)(12 * (Math.Log(freq) / Log_2 - Log2_440) + Note440Hz + 0.5);
                note = vol == 0 ? -1 : note;
                channel.note = note;
                channel.volume = Math.Min((int)(vol * 1.33), 19);
                channel.pantp = (reg[3 + i * 4] & 0xf8) >> 3; // Length counter load
                channel.kf = (reg[i * 4] & 0xc0) >> 6; // Duty
                channel.dda = ((reg[i * 4] & 0x20) >> 5) != 0; // LengthCounter
                channel.noise = ((reg[i * 4] & 0x10) >> 4) != 0; // constantVolume
            }

            newParam.pcmChannel.dda = (reg[8] & 0x80) != 0;
            newParam.pcmChannel.noise = (reg[8] & 0x01) != 0;
            newParam.pcmChannel.note = reg[9] & 0xff;
            newParam.pcmChannel.volume = (reg[9] & 0xff) >> 3;
            newParam.pcmChannel.volume = Math.Min(newParam.pcmChannel.volume, 19);
        }

        // frmMMC5.cs:123 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int i = 0; i < 2; i++)
            {
                MDChipParams.Channel oc = oldParam.sqrChannels[i];
                MDChipParams.Channel nc = newParam.sqrChannels[i];

                DrawBuffMmc5.KeyBoard(screen, i * 2, ref oc.note, nc.note);
                DrawBuffMmc5.Volume(screen, 256, 8 + i * 2 * 8, 0, ref oc.volume, nc.volume);
                DrawBuffMmc5.Font4Int2(screen, 22 * 4, (2 + i * 2) * 8, ref oc.pantp, nc.pantp);
                DrawBuffMmc5.DrawDuty(screen, 24, (1 + i * 2) * 8, ref oc.kf, nc.kf);
                DrawBuffMmc5.DrawNesSw(screen, 32, (2 + i * 2) * 8, ref oc.dda, nc.dda);
                DrawBuffMmc5.DrawNesSw(screen, 40, (2 + i * 2) * 8, ref oc.noise, nc.noise);
                DrawBuffMmc5.ChMmc5(screen, i, ref oc.mask, nc.mask);
            }

            DrawBuffMmc5.Volume(screen, 256, 8 + 3 * 8, 0, ref oldParam.pcmChannel.volume, newParam.pcmChannel.volume);
            DrawBuffMmc5.DrawNesSw(screen, 148, 32, ref oldParam.pcmChannel.dda, newParam.pcmChannel.dda);
            DrawBuffMmc5.DrawNesSw(screen, 160, 32, ref oldParam.pcmChannel.noise, newParam.pcmChannel.noise);
            DrawBuffMmc5.Font4HexByte(screen, 196, 32, ref oldParam.pcmChannel.note, newParam.pcmChannel.note);
            DrawBuffMmc5.ChMmc5(screen, 2, ref oldParam.pcmChannel.mask, newParam.pcmChannel.mask);

            screen.Present();
        }
    }
}
