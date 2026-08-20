// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmK054539.cs - the Konami K054539 8-channel PCM
// sample player (with per-channel pitch, reverb delay/depth, and stereo pan) visualizer. An
// unusual two-band layout on one shared background: rows 1-8 show keyboard/note, a 2-tile
// pan icon, and independent L/R volume LED bars; rows 10-17 (same 8 channels) show hex
// readouts of start/loop addresses, pitch, reverb delay, PCM type, loop/reverse flags, volume,
// and key-on/key-off flags.
//
// Data source: reads chipRegister.GetK054539State(chipId) directly - already a public
// ChipRegister method (mirroring Audio.GetK054539State, which just forwards to
// mds.ReadK054539Status on Windows), so no new getter was needed for this chip.
//
// Clock handling: frmK054539.cs's private searchK054539Note divides by Audio.ClockK054539, a
// dynamic per-format static property whose default is 0 in Audio.cs (like GA20's ClockGA20,
// unlike most other chips' nonzero defaults). This port substitutes the clockHz constructor
// parameter, with the fallback left at 0 (matching Audio.cs's own default) rather than
// inventing a nonzero one - if clockHz is ever actually 0, `hz` becomes 0 and no note is ever
// resolved above index 0, exactly reproducing the original's behavior in that case.
//
// Preserved quirk: like every other search*Note helper in the PCM family (C140/C352/GA20/
// K053260), searchK054539Note keeps overwriting (m, n) on every iteration where hz > a instead
// of finding a global minimum distance - the local "m" is assigned but never compared against.
// Also preserves the `+ 1` offset applied to the returned note index (`return n + 1`), which -
// like C140's own `+ 1` offset - is unique to this chip among the PCM family so far (C352/
// GA20/K053260 all return the bare index).
//
// Preserved quirk: the `pan` register byte is remapped through a 15-entry lookup table
// (`pantbl`) after being range-clamped into one of two byte ranges (0x81-0x8f or 0x11-0x1f,
// falling back to a hardcoded middle value 0x18-0x11 otherwise) - ported verbatim, including
// the odd literal `0x18 - 0x11` fallback expression (evaluates to 7, but kept as-is rather than
// inlined to a bare literal).
//
// Deliberate simplification: `tp` hardcoded 0 (frmK054539.cs's own screenInit also computes it
// from a commented-out/disabled "UseScci" setting - `bool K054539Type = false;`
// unconditionally), same as every other DrawBuffXxx.cs in this port. screenInit's placeholder
// pre-draw loop is skipped, same as every other chip in this port - the first real
// ScreenDrawParams call naturally draws everything via the normal dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class K054539Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly uint clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.K054539 newParam = new();
        private readonly MDChipParams.K054539 oldParam = new();

        private static readonly int[] PanTbl = new int[]
        {
            0 * 5 + 4, 1 * 5 + 4, 1 * 5 + 4, 2 * 5 + 4, 2 * 5 + 4, 3 * 5 + 4, 3 * 5 + 4,
            4 * 5 + 4,
            4 * 5 + 3, 4 * 5 + 3, 4 * 5 + 2, 4 * 5 + 2, 4 * 5 + 1, 4 * 5 + 1, 4 * 5 + 0,
        };

        public PixelScreen Screen => screen;

        public K054539Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffK054539.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeK054539");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmK054539.cs:148 searchK054539Note.
        private int SearchK054539Note(int freq)
        {
            int clock = (int)clockHz;
            if (clock >= 1000000) clock /= 384;
            int hz = (int)(clock / (0x10000 / (double)freq));
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
            return n + 1;
        }

        // frmK054539.cs:180 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.K054539.k054539_state k054539Register = chipRegister.GetK054539State(ChipID);
            if (k054539Register == null) return;

            byte[] regs = k054539Register.regs;

            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];

                byte pan = regs[0x20 * ch + 0x05];
                if (pan >= 0x81 && pan <= 0x8f) pan -= 0x81;
                else if (pan >= 0x11 && pan <= 0x1f) pan -= 0x11;
                else pan = 0x18 - 0x11;
                channel.pan = PanTbl[pan];

                channel.sadr = regs[0x20 * ch + 0x0c]
                    + (regs[0x20 * ch + 0x0d] << 8)
                    + (regs[0x20 * ch + 0x0e] << 16);
                channel.eadr = regs[0x20 * ch + 0x08]
                    + (regs[0x20 * ch + 0x09] << 8)
                    + (regs[0x20 * ch + 0x0a] << 16);
                channel.echo = regs[0x20 * ch + 0x00]
                    + (regs[0x20 * ch + 0x01] << 8)
                    + (regs[0x20 * ch + 0x02] << 16);
                channel.freq = regs[0x20 * ch + 0x06]
                    + (regs[0x20 * ch + 0x07] << 8);
                channel.kf = regs[0x20 * ch + 0x04];
                channel.bank = (regs[0x200 + 2 * ch + 0] & 0xc) >> 2;
                channel.loopFlg = (regs[0x200 + 2 * ch + 1] & 0x1) != 0;
                channel.ex = (regs[0x200 + 2 * ch + 0] & 0x20) != 0;
                channel.volume = regs[0x20 * ch + 0x03];
                byte vol = (byte)(0x40 - Common.Range(channel.volume, 0, 0x40));
                channel.dda = (regs[0x214] & (1 << ch)) != 0;
                channel.noise = (regs[0x215] & (1 << ch)) != 0;

                if ((regs[0x22c] & (1 << ch)) != 0)
                {
                    channel.volumeL = Common.Range(vol * 19 * (channel.pan / 5) / 4 / 0x40, 0, 19);
                    channel.volumeR = Common.Range(vol * 19 * (channel.pan % 5) / 4 / 0x40, 0, 19);
                    channel.note = SearchK054539Note(channel.echo);
                }
                else
                {
                    if (channel.volumeL > 0) channel.volumeL--;
                    if (channel.volumeR > 0) channel.volumeR--;
                    channel.note = -1;
                }
            }
        }

        // frmK054539.cs:237 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffK054539.Font4Hex24Bit(screen, 4 * 9, ch * 8 + 8 * 10, ref oyc.sadr, nyc.sadr);
                DrawBuffK054539.Font4Hex24Bit(screen, 4 * 17, ch * 8 + 8 * 10, ref oyc.eadr, nyc.eadr);
                DrawBuffK054539.Font4Hex24Bit(screen, 4 * 25, ch * 8 + 8 * 10, ref oyc.echo, nyc.echo);
                DrawBuffK054539.Font4Hex16Bit(screen, 4 * 33, ch * 8 + 8 * 10, ref oyc.freq, nyc.freq);
                DrawBuffK054539.Font4HexByte(screen, 4 * 39, ch * 8 + 8 * 10, ref oyc.kf, nyc.kf);
                DrawBuffK054539.Font4HexByte(screen, 4 * 43, ch * 8 + 8 * 10, ref oyc.bank, nyc.bank);
                DrawBuffK054539.DrawNesSw(screen, 4 * 46, ch * 8 + 8 * 10, ref oyc.loopFlg, nyc.loopFlg);
                DrawBuffK054539.DrawNesSw(screen, 4 * 48, ch * 8 + 8 * 10, ref oyc.ex, nyc.ex);
                DrawBuffK054539.Font4HexByte(screen, 4 * 51, ch * 8 + 8 * 10, ref oyc.volume, nyc.volume);
                DrawBuffK054539.DrawNesSw(screen, 4 * 54, ch * 8 + 8 * 10, ref oyc.dda, nyc.dda);
                DrawBuffK054539.DrawNesSw(screen, 4 * 56, ch * 8 + 8 * 10, ref oyc.noise, nyc.noise);

                DrawBuffK054539.PanType4(screen, 4 * 6, ch * 8 + 8 * 1, ref oyc.pan, nyc.pan);
                DrawBuffK054539.KeyBoard(screen, ch, ref oyc.note, nyc.note);
                DrawBuffK054539.Volume(screen, 4 * 64, ch * 8 + 8, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffK054539.Volume(screen, 4 * 64, ch * 8 + 12, 1, ref oyc.volumeR, nyc.volumeR);
                DrawBuffK054539.ChK054539(screen, ch, ref oyc.mask, nyc.mask);
            }

            screen.Present();
        }
    }
}
