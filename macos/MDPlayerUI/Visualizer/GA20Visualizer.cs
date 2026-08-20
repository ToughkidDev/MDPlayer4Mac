// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmGA20.cs - the Irem GA20 4-channel PCM sample
// player visualizer, the simplest chip of the PCM family so far. One 8px row per channel
// across only 4 channels, each showing hex readouts of the live sample start/end/playback-
// position addresses (20-bit) and frequency/volume (byte), a keyboard/note readout, a single
// raw-pixel volume LED bar, and a channel badge.
//
// Data source: reads chipRegister.GetGA20State(chipId)/GetGA20KeyOn(chipId) directly - both
// already public methods on ChipRegister (mirroring Audio.GetGA20State/GetGA20KeyOn, which
// just forward to the same calls on Windows), so no new getter was needed for this chip.
//
// Clock handling: frmGA20.cs's private searchGA20Note divides by Audio.ClockGA20, a dynamic
// per-format static property (default 0 in Audio.cs, unlike every other chip's nonzero
// default - GA20 only ever appears in formats that explicitly set this clock). This port
// substitutes the clockHz constructor parameter, with a fallback of 0 preserved (matching
// Audio.cs's own default) rather than inventing a nonzero fallback - if clockHz is ever
// actually 0 at runtime, `clock / 4` is 0 and `hz` becomes a divide-by-zero, exactly
// reproducing the original's behavior in that (never-expected-in-practice) case.
//
// Preserved quirk: exactly like C140/C352's search*Note helpers, this chip's searchGA20Note
// keeps overwriting (m, n) on every iteration where hz > a instead of finding a global minimum
// distance - the local "m" is assigned but never compared against. Ported verbatim.
//
// Preserved quirk: frmGA20.cs's screenDrawParams reuses DrawBuff.ChC352 for GA20's channel
// badge rather than a dedicated function - see DrawBuffGA20.cs's header comment; this port's
// local equivalent is named ChGa20 there.
//
// Deliberate simplification: `tp` hardcoded 0 (frmGA20.cs derives it from a commented-out/
// disabled "UseScci" setting, same as C352's screenInit), same as every other DrawBuffXxx.cs
// in this port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in
// this port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class GA20Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly uint clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.GA20 newParam = new();
        private readonly MDChipParams.GA20 oldParam = new();

        public PixelScreen Screen => screen;

        public GA20Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffGA20.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeGA20");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmGA20.cs:147 searchGA20Note.
        private int SearchGA20Note(int freq)
        {
            int clock = (int)clockHz / 4;
            int hz = clock / (256 - freq);
            double m = double.MaxValue;

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(
                    4000.0
                    * Tables.pcmMulTbl[i % 12 + 12]
                    * System.Math.Pow(2, i / 12 - 3 + 2)
                    );

                if (hz > a)
                {
                    m = a;
                    n = i;
                }
            }
            _ = m; // matches the original's unused-after-assignment local.
            return n;
        }

        // frmGA20.cs:171 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.iremga20.ga20_state ga20Register = chipRegister.GetGA20State(ChipID);
            bool[] ga20KeyOn = chipRegister.GetGA20KeyOn(ChipID);
            if (ga20Register == null) return;

            for (int ch = 0; ch < 4; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];

                channel.freq = ga20Register.regs[4 + (ch << 3)];
                channel.sadr = (int)ga20Register.channel[ch].start;
                channel.eadr = (int)ga20Register.channel[ch].end;
                channel.ladr = (int)ga20Register.channel[ch].pos;
                channel.volume = ga20Register.regs[5 + (ch << 3)];
                channel.note = SearchGA20Note(channel.freq);

                if (ga20KeyOn[ch])
                {
                    channel.volumeL = Common.Range((256 - ga20Register.regs[5 + (ch << 3)]) / 13, 0, 19);
                    ga20KeyOn[ch] = false;
                }
                else
                {
                    if (channel.volumeL > 0) channel.volumeL--;
                    else channel.note = -1;
                }
            }
        }

        // frmGA20.cs:205 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 4; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffGA20.Font4Hex20Bit(screen, 4 * 65, ch * 8 + 8, ref oyc.sadr, nyc.sadr);
                DrawBuffGA20.Font4Hex20Bit(screen, 4 * 71, ch * 8 + 8, ref oyc.eadr, nyc.eadr);
                DrawBuffGA20.Font4Hex20Bit(screen, 4 * 77, ch * 8 + 8, ref oyc.ladr, nyc.ladr);
                DrawBuffGA20.Font4HexByte(screen, 4 * 83, ch * 8 + 8, ref oyc.freq, nyc.freq);
                DrawBuffGA20.Font4HexByte(screen, 4 * 86, ch * 8 + 8, ref oyc.volume, nyc.volume);
                DrawBuffGA20.KeyBoardToGA20(screen, ch, ref oyc.note, nyc.note);
                DrawBuffGA20.Volume(screen, 4 * 88, ch * 8 + 8, 0, ref oyc.volumeL, nyc.volumeL);
                DrawBuffGA20.ChGa20(screen, ch, ref oyc.mask, nyc.mask);
            }

            screen.Present();
        }
    }
}
