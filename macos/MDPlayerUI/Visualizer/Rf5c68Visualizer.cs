// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmRf5c68.cs - the standalone arcade-board Ricoh
// RF5C68 8-channel PCM sample-player visualizer (cousin of the Sega Mega-CD's built-in
// RF5C164 - same "Ricoh 8-channel PCM" chip family, but backed by a genuinely different
// MDSound.rf5c68 emulation core, not MDSound.scd_pcm). One 8px row per channel across 8
// channels, each showing a keyboard/note readout, independent L/R volume LED bars, a
// byte-packed 2-tile pan icon, and a channel badge.
//
// Data source: reads chipRegister.GetRf5c68Register(chipId) - a new one-line forward added to
// ChipRegister.cs, forwarding to mds.ReadRf5c68Register (mirroring Audio.GetRf5c68Register -
// this port's ChipRegister keeps mds private, same reason a wrapper was needed for
// GetRf5c164Register/GetK053260Register before it).
//
// No clock handling needed - like MegaCD/RF5C164's searchRf5c164Note, this chip's
// searchRf5c68Note does NOT divide by any Audio.ClockXxx property; it compares the raw `step`
// frequency register directly against a pcmMulTbl-based table, and performs a genuine,
// honest global-minimum search (not the "keeps overwriting on every freq > a" quirk shared by
// C140/C352/GA20/K053260/K054539).
//
// Reuses the exact same background sprite (planeC) as MegaCD/RF5C164's visualizer - the
// original Windows source shares that art between both chips too.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Rf5c68Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly int ChipID;

        private readonly MDChipParams.RF5C68 newParam = new();
        private readonly MDChipParams.RF5C68 oldParam = new();

        public PixelScreen Screen => screen;

        public Rf5c68Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            _ = clockHz; // frmRf5c68.cs's screenChangeParams never reads a clock value.

            DrawBuffRf5c68.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeC");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();

            for (int c = 0; c < newParam.channels.Length; c++)
            {
                newParam.channels[c].note = -1;
                newParam.channels[c].volumeL = -1;
                newParam.channels[c].volumeR = -1;
                newParam.channels[c].pan = -1;
            }
        }

        // frmRf5c68.cs:182 searchRf5c68Note.
        private static int SearchRf5c68Note(uint freq)
        {
            double m = double.MaxValue;
            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                double a = System.Math.Abs(freq - (0x0800 * Tables.pcmMulTbl[i % 12 + 12] * System.Math.Pow(2, ((int)(i / 12) - 4))));
                if (m > a)
                {
                    m = a;
                    n = i;
                }
            }
            return n;
        }

        // frmRf5c68.cs:87 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.rf5c68.rf5c68_state rf5c68Register = chipRegister.GetRf5c68Register(ChipID);
            if (rf5c68Register == null) return;

            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                MDSound.rf5c68.pcm_channel pc = rf5c68Register.chan[ch];

                if (nyc.volume > 0) nyc.volume--;

                if (pc.enable != 0)
                {
                    nyc.note = SearchRf5c68Note(pc.step);
                    if (pc.keyOn)
                    {
                        nyc.volume = pc.env;
                        pc.keyOn = false;
                    }
                    int mulL = (nyc.volume * (pc.pan & 0x0F)) >> 5;
                    int mulR = (nyc.volume * (pc.pan >> 4)) >> 5;
                    nyc.volumeL = System.Math.Min(System.Math.Max(mulL / 3, 0), 19);
                    nyc.volumeR = System.Math.Min(System.Math.Max(mulR / 3, 0), 19);
                }
                else
                {
                    nyc.volume = 0;
                    nyc.volumeL = 0;
                    nyc.volumeR = 0;
                }

                if (nyc.volumeL == 0 && nyc.volumeR == 0)
                {
                    nyc.note = -1;
                }
                else if (!pc.key)
                {
                    nyc.note = -1;
                    nyc.volume = 0;
                    nyc.volumeL = 0;
                    nyc.volumeR = 0;
                }

                nyc.pan = pc.pan;
            }
        }

        // frmRf5c68.cs:131 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 8; c++)
            {
                MDChipParams.Channel orc = oldParam.channels[c];
                MDChipParams.Channel nrc = newParam.channels[c];

                DrawBuffRf5c68.Volume(screen, 256, 8 + c * 8, 1, ref orc.volumeL, nrc.volumeL);
                DrawBuffRf5c68.Volume(screen, 256, 8 + c * 8, 2, ref orc.volumeR, nrc.volumeR);
                DrawBuffRf5c68.KeyBoard(screen, c, ref orc.note, nrc.note);
                DrawBuffRf5c68.PanType2(screen, c, ref orc.pan, nrc.pan);
                DrawBuffRf5c68.ChRf5c164(screen, c, ref orc.mask, nrc.mask);
            }

            screen.Present();
        }
    }
}
