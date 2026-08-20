// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmN106.cs - the N106 (Namco 163/106) cartridge
// mapper's expansion audio channel visualizer. Up to 8 wavetable-synthesis channels, each a
// uniform 24px-tall row: enable/key-on switches, hex volume/frequency readouts, a piano-key
// note readout, and a live waveform strip drawn straight from the channel's own wavetable
// RAM (which is why the register-decode step below builds a 280-sample scrolling buffer
// rather than reading a fixed-size table like FDS's 32-sample wavetables).
//
// Channel display order is REVERSED (`rev = true` in the original, hardcoded here since the
// original never exposes a way to change it): channel 0 is drawn using data from register
// index 7, channel 7 from register index 0.
//
// Data source: reads chipRegister.getN106Register(chipID) - already-decoded
// MDSound.np.chip.TrackInfoN106[] track-info objects, the same already-public getter used
// directly (no new wrapper needed, unlike VRC6/VRC7's getters).
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop (blank piano keys) is skipped, same as every
// other chip in this port - the first real ScreenDrawParams call naturally draws everything
// via the normal dirty-diff path.
using MDPlayer;
using MDSound.np.chip;

namespace MDPlayer.UI.Visualizer
{
    public sealed class N106Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.N106 newParam = new();
        private readonly MDChipParams.N106 oldParam = new();

        public PixelScreen Screen => screen;

        public N106Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmN106.cs's screenChangeParams never reads a clock value.

            DrawBuffN106.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeN106");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmN106.cs:159 screenChangeParams.
        public void ScreenChangeParams()
        {
            TrackInfoN106[] info = (TrackInfoN106[])chipRegister.getN106Register(ChipID);
            if (info == null) return;

            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];

                nyc.bit[0] = info[ch].GetKeyStatus();
                nyc.bit[1] = info[ch].GetHalt();

                int v = info[ch].GetVolume() * 2;
                nyc.volume = System.Math.Min(v, 19);
                nyc.volumeR = info[ch].GetVolume();

                nyc.freq = (int)info[ch].GetFreq();
                v = info[ch].GetNote(info[ch].GetFreqHz()) - 4 * 12;
                nyc.note = nyc.volumeL == 0 || !nyc.bit[0] ? -1 : v;

                nyc.bank = info[ch].wavelen & 127;
                nyc.bank = nyc.bank <= 0 ? (info[ch].wavelen > 127 ? 127 : 0) : nyc.bank;
                if (nyc.aryWave16bit == null) nyc.aryWave16bit = new short[280];
                for (int i = 0; i < 280; i++)
                {
                    if (i < nyc.bank)
                    {
                        nyc.aryWave16bit[i] = info[ch].wave[i];
                    }
                    else
                    {
                        if (i != 279)
                        {
                            nyc.aryWave16bit[i] = nyc.aryWave16bit[i + 1];
                        }
                        else
                        {
                            ushort w = (ushort)(((sbyte)info[ch].GetOutput() >> 4) + 8);
                            nyc.aryWave16bit[i] = (short)(w + 16);
                        }
                    }
                }
            }
        }

        // frmN106.cs:204 screenDrawParams. `rev` (always true) reverses the channel display
        // order - see file header.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 8; ch++)
            {
                int vch = 7 - ch;
                MDChipParams.Channel oyc = oldParam.channels[vch];
                MDChipParams.Channel nyc = newParam.channels[vch];

                DrawBuffN106.DrawNesSw(screen, 6 * 4, ch * 24 + 8, ref oyc.bit[1], nyc.bit[1]);
                DrawBuffN106.DrawNesSw(screen, 7 * 4, ch * 24 + 8, ref oyc.bit[0], nyc.bit[0]);

                DrawBuffN106.Volume(screen, 256, 8 + ch * 3 * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffN106.Font4Hex4Bit(screen, 4 * 4, ch * 24 + 16, ref oyc.volumeR, nyc.volumeR);

                DrawBuffN106.Font4Hex20Bit(screen, 4 * 4, ch * 24 + 24, ref oyc.freq, nyc.freq);

                DrawBuffN106.KeyBoard(screen, ch * 3, ref oyc.note, nyc.note);

                if (oyc.aryWave16bit == null && nyc.aryWave16bit != null) oyc.aryWave16bit = new short[nyc.aryWave16bit.Length];
                DrawBuffN106.WaveFormToN106(screen, 10 * 4, ch * 24 + 16, ref oyc.aryWave16bit, nyc.aryWave16bit);
                DrawBuffN106.ChN163(screen, ch, ref oyc.mask, nyc.mask);
            }

            screen.Present();
        }
    }
}
