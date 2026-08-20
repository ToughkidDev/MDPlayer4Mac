// Port of MDPlayer/MDPlayerx64/form/KB/WF/frmK051649.cs - the Konami SCC (K051649)
// 5-channel wavetable-synthesis chip visualizer, laid out 3 channels on the top row and 2 on
// the bottom row (x = c % 3, y = c / 3). Each channel shows a keyboard/note readout, a raw
// volume LED bar, the current playback frequency and volume as hex digits, a DDA (key-on)
// status icon, a graphical 32-sample waveform bar-graph, and a 32-cell hex-byte grid of the
// same wavetable RAM.
//
// Data source: reads chipRegister.GetK051649Register(chipID) - MDSound.K051649.k051649_state,
// a new one-line forward added to ChipRegister.cs alongside DrawBuffK051649.cs.
//
// Clock handling: frmK051649.cs's screenChangeParams divides by Audio.ClockK051649, a dynamic
// per-format static property (default 1500000 in Audio.cs) rather than a fixed constant. This
// port substitutes the clockHz constructor parameter instead, falling back to 1500000 when
// clockHz is 0, following the same pattern already used in Sn76489Visualizer.cs/
// S5bVisualizer.cs/Ay8910Visualizer.cs.
//
// frmK051649.cs's own private searchSSGNote is textually identical to Common.searchSSGNote
// (same 12*8 table scan, same distance formula, no early break) - unlike DMG's genuinely
// different private copy - so this port calls Common.searchSSGNote directly instead of
// duplicating it, per the decision documented in DrawBuffK051649.cs's header comment.
//
// Deliberate simplification: `tp` hardcoded 0 (frmK051649.cs derives it from a "use real
// hardware" UI setting this port doesn't carry), same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in this
// port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class K051649Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly uint clockHz;
        private readonly int ChipID;

        private readonly MDChipParams.K051649 newParam = new();
        private readonly MDChipParams.K051649 oldParam = new();

        public PixelScreen Screen => screen;

        public K051649Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffK051649.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeK051649");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmK051649.cs:88 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.K051649.k051649_state chip = chipRegister.GetK051649Register(ChipID);
            if (chip == null) return;

            float clock = clockHz != 0 ? clockHz : 1500000.0f;

            for (int ch = 0; ch < 5; ch++)
            {
                MDSound.K051649.k051649_sound_channel psg = chip.channel_list[ch];
                if (psg == null) continue;

                MDChipParams.Channel channel = newParam.channels[ch];
                for (int i = 0; i < 32; i++) channel.inst[i] = psg.waveram[i];

                float ftone = clock / (8.0f * psg.frequency);
                channel.freq = psg.frequency;
                channel.volume = psg.key != 0 ? (int)(psg.volume * 1.33) : 0;
                channel.volumeL = psg.volume;
                channel.note = (psg.key != 0 && channel.volume != 0) ? Common.searchSSGNote(ftone) : -1;
                channel.dda = psg.key != 0;
            }
        }

        // frmK051649.cs:110 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 5; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];
                int x = c % 3;
                int y = c / 3;

                DrawBuffK051649.KeyBoard(screen, c, ref oyc.note, nyc.note);
                DrawBuffK051649.Volume(screen, 256, 8 + c * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffK051649.Font4Hex12Bit(screen, x * 4 * 26 + 4 * 14, y * 8 * 6 + 8 * 11, ref oyc.freq, nyc.freq);
                DrawBuffK051649.Font4Hex4Bit(screen, x * 4 * 26 + 4 * 22, y * 8 * 6 + 8 * 11, ref oyc.volumeL, nyc.volumeL);
                DrawBuffK051649.DrawNesSw(screen, x * 4 * 26 + 4 * 25, y * 8 * 6 + 8 * 11, ref oyc.dda, nyc.dda);
                DrawBuffK051649.WaveFormToK051649(screen, c, ref oyc.typ, nyc.inst);

                DrawBuffK051649.ChK051649(screen, c, ref oyc.mask, nyc.mask);

                for (int i = 0; i < 32; i++)
                {
                    int fx = i % 8;
                    int fy = i / 8;
                    DrawBuffK051649.Font4HexByte(screen, x * 4 * 26 + 4 * 10 + fx * 8, y * 8 * 6 + 8 * 7 + fy * 8, ref oyc.inst[i], nyc.inst[i]);
                }
            }

            screen.Present();
        }
    }
}
