// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmPCM8.cs - the X68000's "PCM8" 8-voice sample
// driver convention visualizer, used by the MXDRV/ZMS/RCS sequenced-music formats. Same
// architectural shape as MpcmX68kVisualizer.cs: the original reads driver-internal state
// directly (Audio.DriverVirtual cast to MXDRV/ZMS/RCS and its public pcm8St field) rather than
// through ChipRegister, so this port likewise takes the live baseDriver directly instead of a
// ChipRegister, with a non-standard (baseDriver driver) constructor.
//
// DIFFERENCE FROM MpcmX68k: the original also branches on Audio.DriverVirtual being RCS (a
// third sequenced-music driver format), but this port has never ported an RCS driver class at
// all (no macos/MDPlayerCore/Driver/RCS directory exists) - there is nothing to pattern-match
// against, so that branch is omitted entirely rather than kept as unreachable dead code
// referencing a nonexistent type. MXDRV and ZMS (both ported) are handled exactly as the
// original does.
//
// Genuinely-preserved original quirk: MDChipParams.PCM8 has Channel[16], and
// screenDrawParams always draws all 16 rows, but screenChangeParams only ever updates
// channels 0..pcm8St.Length-1 (pcm8St is a fixed 8-element array on both MXDRV and ZMS) - so
// channels 8-15 are always idle/default in this chip's display. This isn't a bug introduced
// by the port; frmPCM8.cs's own screenChangeParams loops `ch < pcm8St.Length` (8) while
// screenDrawParams loops `c < 16`.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class PCM8Visualizer
    {
        private readonly PixelScreen screen;
        private readonly MDPlayer.baseDriver driver;

        private readonly MDChipParams.PCM8 newParam = new();
        private readonly MDChipParams.PCM8 oldParam = new();

        public PixelScreen Screen => screen;

        public PCM8Visualizer(MDPlayer.baseDriver driver)
        {
            this.driver = driver;

            DrawBuffPCM8.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planePCM8");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmPCM8.cs:88 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDPlayer.Driver.MXDRV.MXDRV.Pcm8St[] pcm8St;
            if (driver is MDPlayer.Driver.MXDRV.MXDRV mdx)
            {
                pcm8St = mdx.pcm8St;
            }
            else if (driver is MDPlayer.Driver.ZMS.ZMS zms)
            {
                pcm8St = zms.pcm8St;
            }
            else
            {
                return;
            }

            for (int ch = 0; ch < pcm8St.Length; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                if (pcm8St[ch].Keyon)
                {
                    nyc.volume = System.Math.Min(System.Math.Max((int)(((pcm8St[ch].mode >> 16) & 0x0f) * 20.0 / 16.0), 0), 19);
                    nyc.volumeL = (int)((pcm8St[ch].mode >> 16) & 0xff);
                    nyc.freq = (int)((pcm8St[ch].mode >> 8) & 0xff);
                    nyc.pcmMode = (int)((pcm8St[ch].mode >> 0) & 0xff);
                    nyc.utp = pcm8St[ch].tablePtr;
                    nyc.utl = pcm8St[ch].length;
                    nyc.pan = nyc.pcmMode == 1 ? 2 : (nyc.pcmMode == 2 ? 1 : nyc.pcmMode);
                    pcm8St[ch].Keyon = false;
                }
                else
                {
                    if (nyc.volume > 0) nyc.volume--;
                }
            }
        }

        // frmPCM8.cs:132 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 16; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffPCM8.ChPCM8(screen, c, ref oyc.mask, nyc.mask);

                DrawBuffPCM8.Pan(screen, 36, 8 + c * 8, ref oyc.pan, System.Math.Clamp(nyc.pan, 0, 3), ref oyc.pantp, 0);

                int x = 10;
                DrawBuffPCM8.Font4Hex32Bit(screen, (x + 3) * 4, c * 8 + 8, ref oyc.utp, nyc.utp); // ptr
                DrawBuffPCM8.Font4Hex32Bit(screen, (x + 13) * 4, c * 8 + 8, ref oyc.utl, nyc.utl); // length

                DrawBuffPCM8.Font4Int2(screen, (x + 22) * 4, c * 8 + 8, ref oyc.pcmMode, nyc.pcmMode); // mode
                DrawBuffPCM8.Font4Int2(screen, (x + 25) * 4, c * 8 + 8, ref oyc.freq, nyc.freq); // rate
                DrawBuffPCM8.Font4Int2(screen, (x + 51) * 4, c * 8 + 8, ref oyc.volumeL, nyc.volumeL); // volume

                DrawBuffPCM8.Volume(screen, (x + 54) * 4, c * 8 + 8, 0, ref oyc.volume, nyc.volume);
            }

            screen.Present();
        }
    }
}
