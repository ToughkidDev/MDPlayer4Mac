// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmC140.cs - the Namco System 2/21/NA-1 C140
// 24-bit PCM sample-player visualizer, the first chip of the PCM family. 24 sample-playback
// channels, one 8px row per channel, each showing a keyboard/note readout, independent L/R
// volume LED bars, a byte-packed L/R pan icon, two on/off flag icons, and hex readouts of the
// live sample frequency, ROM bank, and start/end/loop addresses.
//
// Data source: reads chipRegister.pcmRegisterC140[chipId] (a 24*16-byte flat register image)
// and chipRegister.pcmKeyOnC140[chipId] (a 24-entry live key-on flag array) directly - both
// are already public ChipRegister fields (Audio.GetC140Register/GetC140KeyOn just forward to
// the same fields on Windows), so no new getter was needed for this chip.
//
// Preserved quirk: pcmKeyOnC140[ch] is a live reference shared with the register-write side
// (ChipRegister.writeC140 sets it true on key-on) - ScreenChangeParams clears it back to
// false after consuming it each frame, exactly as frmC140.cs's screenChangeParams does via
// Audio.GetC140KeyOn's direct (non-copied) array reference.
//
// Clock handling: frmC140.cs's private searchC140Note divides by Audio.ClockC140, a dynamic
// per-format static property (default 21390 in Audio.cs - much lower than most other chips'
// clocks, since C140 addresses sample ROM in fixed steps rather than running at its input
// clock rate). This port substitutes the clockHz constructor parameter instead, falling back
// to 21390 when clockHz is 0, following the same pattern already used in Sn76489Visualizer.cs/
// S5bVisualizer.cs/Ay8910Visualizer.cs/K051649Visualizer.cs.
//
// Preserved quirk: searchC140Note's search loop keeps overwriting (m, n) on every iteration
// where freq > a rather than searching for a global minimum distance (unlike Common.
// searchSSGNote) - the local "m" is assigned but never used as a comparison bound. Ported
// verbatim rather than "fixed" into a closest-match search, per this port's philosophy of
// preserving original behavior/quirks. Also note the +1 offset applied to the returned note
// index in screenChangeParams (`searchC140Note(frequency) + 1`), unlike every other chip's
// bare 0-based note index - preserved as-is.
//
// Deliberate simplification: `tp` hardcoded 0 (frmC140.cs derives it from a "use real
// hardware" UI setting this port doesn't carry), same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in this
// port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class C140Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly uint clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.C140 newParam = new();
        private readonly MDChipParams.C140 oldParam = new();

        public PixelScreen Screen => screen;

        public C140Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffC140.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeF");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmC140.cs:129 searchC140Note.
        private int SearchC140Note(int freq)
        {
            double m = double.MaxValue;

            int clock = (int)(clockHz != 0 ? clockHz : 21390);
            if (clock >= 1000000) clock /= 384;

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(
                    65536.0
                    / 2.0
                    / clock
                    * 8000.0
                    * Tables.pcmMulTbl[i % 12 + 12]
                    * Math.Pow(2, i / 12 - 3)
                    );
                if (freq > a)
                {
                    m = a;
                    n = i;
                }
            }
            _ = m; // matches the original's unused-after-assignment local.
            return n;
        }

        // frmC140.cs:181 screenChangeParams.
        public void ScreenChangeParams()
        {
            byte[] c140State = chipRegister.pcmRegisterC140[ChipID];
            bool[] c140KeyOn = chipRegister.pcmKeyOnC140[ChipID];
            if (c140State == null) return;

            for (int ch = 0; ch < 24; ch++)
            {
                int frequency = c140State[ch * 16 + 2] * 256 + c140State[ch * 16 + 3];
                int l = c140State[ch * 16 + 1];
                int r = c140State[ch * 16 + 0];

                MDChipParams.Channel channel = newParam.channels[ch];
                channel.note = SearchC140Note(frequency) + 1;
                if (c140KeyOn[ch])
                {
                    channel.volumeL = Math.Min(Math.Max((int)(l / 13.4) * 3, 0), 19);
                    channel.volumeR = Math.Min(Math.Max((int)(r / 13.4) * 3, 0), 19);
                }
                else
                {
                    channel.volumeL -= channel.volumeL > 0 ? 1 : 0;
                    channel.volumeR -= channel.volumeR > 0 ? 1 : 0;
                    if (channel.volumeL == 0 && channel.volumeR == 0)
                    {
                        if (c140State[ch * 16 + 5] == 0) channel.note = -1;
                        channel.volumeL = 0;
                        channel.volumeR = 0;
                    }
                }
                channel.pan = ((l >> 4) & 0xf) | (((r >> 4) & 0xf) << 4);

                c140KeyOn[ch] = false;

                channel.freq = (c140State[ch * 16 + 2] << 8) | c140State[ch * 16 + 3];
                if (channel.freq == 0) channel.note = -1;
                channel.bank = c140State[ch * 16 + 4];
                byte d = c140State[ch * 16 + 5];
                channel.bit[0] = (d & 0x10) != 0;
                channel.bit[1] = (d & 0x08) != 0;
                channel.sadr = (c140State[ch * 16 + 6] << 8) | c140State[ch * 16 + 7];
                channel.eadr = (c140State[ch * 16 + 8] << 8) | c140State[ch * 16 + 9];
                channel.ladr = (c140State[ch * 16 + 10] << 8) | c140State[ch * 16 + 11];
            }
        }

        // frmC140.cs:231 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 24; c++)
            {
                MDChipParams.Channel orc = oldParam.channels[c];
                MDChipParams.Channel nrc = newParam.channels[c];

                DrawBuffC140.VolumeToC140(screen, c, 1, ref orc.volumeL, nrc.volumeL);
                DrawBuffC140.VolumeToC140(screen, c, 2, ref orc.volumeR, nrc.volumeR);
                DrawBuffC140.KeyBoardToC140(screen, c, ref orc.note, nrc.note);
                DrawBuffC140.PanType2(screen, c, ref orc.pan, nrc.pan);

                DrawBuffC140.ChC140(screen, c, ref orc.mask, nrc.mask);

                DrawBuffC140.DrawNesSw(screen, 64 * 4, c * 8 + 8, ref orc.bit[0], nrc.bit[0]);
                DrawBuffC140.DrawNesSw(screen, 65 * 4, c * 8 + 8, ref orc.bit[1], nrc.bit[1]);
                DrawBuffC140.Font4Hex16Bit(screen, 4 * 67, c * 8 + 8, ref orc.freq, nrc.freq);
                DrawBuffC140.Font4HexByte(screen, 4 * 72, c * 8 + 8, ref orc.bank, nrc.bank);
                DrawBuffC140.Font4Hex16Bit(screen, 4 * 75, c * 8 + 8, ref orc.sadr, nrc.sadr);
                DrawBuffC140.Font4Hex16Bit(screen, 4 * 80, c * 8 + 8, ref orc.eadr, nrc.eadr);
                DrawBuffC140.Font4Hex16Bit(screen, 4 * 85, c * 8 + 8, ref orc.ladr, nrc.ladr);
            }

            screen.Present();
        }
    }
}
