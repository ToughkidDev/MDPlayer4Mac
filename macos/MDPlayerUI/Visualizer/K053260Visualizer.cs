// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmK053260.cs - the Konami K053260 4-channel PCM
// sample-player visualizer. One 8px row per channel across 4 channels, each showing hex
// readouts of frequency/bank/start/size/pan/volume, 4 on/off flag icons (play/dir/loop/ppcm),
// independent single-tile L/R pan icons, independent L/R volume LED bars, and a
// keyboard/note readout.
//
// Data source: reads chipRegister.GetK053260Register(chipId) - a new one-line forward added to
// ChipRegister.cs alongside DrawBuffK053260.cs, forwarding to mds.getK053260State (mirroring
// Audio.GetK053260Register - Windows' Audio.cs holds mds as a static field and calls it
// directly, but this port's ChipRegister keeps mds private, so a wrapper was needed here, same
// as GetHuC6280Register/GetDMGRegister before it).
//
// Clock handling: frmK053260.cs's private searchNote divides by Audio.ClockK053260, a dynamic
// per-format static property (default 3579545 in Audio.cs). This port substitutes the clockHz
// constructor parameter instead, falling back to 3579545 when clockHz is 0, following the same
// pattern used for every other chip whose original code read a dynamic Audio.ClockXxx property.
//
// Preserved quirk: unlike C140/C352/GA20's search*Note helpers (which all have the same "keeps
// overwriting (m,n) without a true global-minimum search" quirk), this chip's searchNote uses
// the identical pattern too - ported verbatim, including the final
// `Math.Min(Math.Max(n - 2, 0), 95)` note-index clamp/shift, which is unique to this chip
// among the search*Note family ported so far.
//
// Deliberate simplification: `tp` hardcoded 0 (frmK053260.cs's own screenInit/screenDrawParams
// already always pass a literal 0 for tp, so no UI setting was even being read here on
// Windows), same as every other DrawBuffXxx.cs in this port. screenInit's placeholder pre-draw
// loop (piano keys, PanType5 for L/R) is skipped, same as every other chip in this port - the
// first real ScreenDrawParams call naturally draws everything via the normal dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class K053260Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly uint clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.K053260 newParam = new();
        private readonly MDChipParams.K053260 oldParam = new();

        public PixelScreen Screen => screen;

        public K053260Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffK053260.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeK053260");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmK053260.cs:184 searchNote.
        private int SearchNote(uint freq)
        {
            double m = double.MaxValue;

            int clock = (int)(clockHz != 0 ? clockHz : 3579545);

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(
                    0x10000
                    * 8000.0
                    * Tables.pcmMulTbl[i % 12 + 12]
                    * System.Math.Pow(2, i / 12 - 3 + 2)
                    / clock
                    * 6
                    * 2
                    );

                if (freq > a)
                {
                    m = a;
                    n = i;
                }
            }
            _ = m; // matches the original's unused-after-assignment local.
            return System.Math.Min(System.Math.Max(n - 2, 0), 95);
        }

        // frmK053260.cs:143 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.K053260.k053260_state regs = chipRegister.GetK053260Register(ChipID);
            if (regs == null) return;

            for (int ch = 0; ch < 4; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];
                MDSound.K053260.k053260_channel rc = regs.channels[ch];

                channel.freq = (int)rc.rate;
                channel.eadr = (int)rc.size;
                channel.sadr = (int)rc.start;
                channel.bank = (int)rc.bank;
                channel.pan = (int)rc.pan;
                channel.panL = (int)(8 - rc.pan);
                channel.panR = (int)rc.pan;
                channel.volume = (int)rc.volume / 2;
                channel.bit[0] = rc.play != 0;
                channel.bit[1] = rc.dir != 0;
                channel.bit[2] = rc.loop != 0;
                channel.bit[3] = rc.ppcm != 0;

                if (channel.bit[0])
                {
                    channel.volumeL = (int)rc.volume * channel.panL / 8 / 5 / 2;
                    channel.volumeL = System.Math.Min(channel.volumeL, 19);
                    channel.volumeR = (int)rc.volume * channel.panR / 8 / 5 / 2;
                    channel.volumeR = System.Math.Min(channel.volumeR, 19);
                }
                else
                {
                    if (channel.volumeL > 0) channel.volumeL--;
                    if (channel.volumeR > 0) channel.volumeR--;
                }
                channel.panL /= 2;
                channel.panR /= 2;

                uint delta = regs.delta_table[channel.freq];
                channel.note = !channel.bit[0] ? -1 : SearchNote(delta);
            }
        }

        // frmK053260.cs:212 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 4; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffK053260.Font4Hex16Bit(screen, 4 * 69 + 1, ch * 8 + 8, ref oyc.freq, nyc.freq);
                DrawBuffK053260.Font4HexByte(screen, 4 * 74 + 1, ch * 8 + 8, ref oyc.bank, nyc.bank);
                DrawBuffK053260.Font4Hex16Bit(screen, 4 * 77 + 1, ch * 8 + 8, ref oyc.sadr, nyc.sadr);
                DrawBuffK053260.Font4Hex16Bit(screen, 4 * 82 + 1, ch * 8 + 8, ref oyc.eadr, nyc.eadr);
                DrawBuffK053260.Font4HexByte(screen, 4 * 87 + 1, ch * 8 + 8, ref oyc.pan, nyc.pan);
                DrawBuffK053260.Font4HexByte(screen, 4 * 90 + 1, ch * 8 + 8, ref oyc.volume, nyc.volume);

                for (int b = 0; b < 4; b++)
                {
                    DrawBuffK053260.DrawNesSw(screen, 64 * 4 + b * 4 + 1, ch * 8 + 8, ref oyc.bit[b], nyc.bit[b]);
                }

                DrawBuffK053260.PanType5(screen, 4 * 6 + 1, ch * 8 + 8, ref oyc.panL, nyc.panL);
                DrawBuffK053260.PanType5(screen, 4 * 7 + 1, ch * 8 + 8, ref oyc.panR, nyc.panR);
                DrawBuffK053260.VolumeXY1(screen, 4 * 92 + 1, ch * 8 + 8, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffK053260.VolumeXY1(screen, 4 * 92 + 1, ch * 8 + 12, 1, ref oyc.volumeR, nyc.volumeR);

                DrawBuffK053260.KeyBoardXYFX(screen, 4 * 8 + 1, 4 * 103 + 1, ch * 8 + 8, ref oyc.note, nyc.note);
                DrawBuffK053260.ChK053260(screen, ch, ref oyc.mask, nyc.mask);
            }

            screen.Present();
        }
    }
}
