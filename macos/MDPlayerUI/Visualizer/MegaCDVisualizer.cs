// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmMegaCD.cs - the Sega Mega-CD/Sega CD's built-in
// RF5C164 8-channel PCM sample-player visualizer (a close cousin of the standalone RF5C68 used
// in arcade boards - same MDSound.scd_pcm chip class backs both). One 8px row per channel
// across 8 channels, each showing a keyboard/note readout, independent L/R volume LED bars, a
// byte-packed 2-tile pan icon, and a channel badge.
//
// Data source: reads chipRegister.GetRf5c164Register(chipId) - a new one-line forward added to
// ChipRegister.cs alongside DrawBuffMegaCD.cs, forwarding to mds.ReadRf5c164Register
// (mirroring Audio.GetRf5c164Register - this port's ChipRegister keeps mds private, same
// reason a wrapper was needed for GetK053260Register/GetHuC6280Register/GetDMGRegister before
// it).
//
// No clock handling needed - unlike every PCM-family chip ported so far (C140/C352/GA20/
// K053260/K054539), frmMegaCD.cs's searchRf5c164Note does NOT divide by any Audio.ClockXxx
// property; it compares the raw Step_B frequency register directly against a pcmMulTbl-based
// table. This is also the first PCM-family search*Note helper that performs a genuine
// global-minimum search (`if (m > a) { m = a; n = i; }`) rather than the "keeps overwriting
// on every freq > a" quirk shared by C140/C352/GA20/K053260/K054539 - ported as an honest
// closest-match search here since that's what the original actually does.
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in this
// port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class MegaCDVisualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly int ChipID;

        private readonly MDChipParams.RF5C164 newParam = new();
        private readonly MDChipParams.RF5C164 oldParam = new();

        public PixelScreen Screen => screen;

        public MegaCDVisualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            _ = clockHz; // frmMegaCD.cs's screenChangeParams never reads a clock value.

            DrawBuffMegaCD.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeC");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmMegaCD.cs:166 searchRf5c164Note.
        private static int SearchRf5c164Note(uint freq)
        {
            double m = double.MaxValue;
            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                double a = System.Math.Abs(freq - 0x0800 * Tables.pcmMulTbl[i % 12 + 12] * System.Math.Pow(2, i / 12 - 4));
                if (m > a)
                {
                    m = a;
                    n = i;
                }
            }
            return n;
        }

        // frmMegaCD.cs:86 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.scd_pcm.pcm_chip_ rf5c164Register = chipRegister.GetRf5c164Register(ChipID);
            if (rf5c164Register == null) return;

            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];
                MDSound.scd_pcm.pcm_chan_ rc = rf5c164Register.Channel[ch];

                if (rc.Enable != 0)
                {
                    channel.note = SearchRf5c164Note(rc.Step_B);
                    channel.volumeL = System.Math.Min(System.Math.Max((int)rc.MUL_L / 3, 0), 19);
                    channel.volumeR = System.Math.Min(System.Math.Max((int)rc.MUL_R / 3, 0), 19);
                }
                else
                {
                    channel.note = -1;
                    channel.volumeL = 0;
                    channel.volumeR = 0;
                }
                channel.pan = (int)rc.PAN;
            }
        }

        // frmMegaCD.cs:114 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 8; c++)
            {
                MDChipParams.Channel orc = oldParam.channels[c];
                MDChipParams.Channel nrc = newParam.channels[c];

                DrawBuffMegaCD.Volume(screen, 256, 8 + c * 8, 1, ref orc.volumeL, nrc.volumeL);
                DrawBuffMegaCD.Volume(screen, 256, 8 + c * 8, 2, ref orc.volumeR, nrc.volumeR);
                DrawBuffMegaCD.KeyBoard(screen, c, ref orc.note, nrc.note);
                DrawBuffMegaCD.PanType2(screen, c, ref orc.pan, nrc.pan);
                DrawBuffMegaCD.ChRf5c164(screen, c, ref orc.mask, nrc.mask);
            }

            screen.Present();
        }
    }
}
