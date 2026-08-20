// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmC352.cs - the Namco C352 32-channel quad-output
// (front L/R + rear L/R) PCM sample-player visualizer. One 8px row per channel across 32
// channels, each showing a keyboard/note readout, four independent volume LED bars (front L/R
// + rear L/R), a byte-packed L/R pan icon, 16 on/off flag icons, and hex readouts of the live
// sample frequency, ROM bank, and start/end/loop addresses.
//
// Data source: reads chipRegister.pcmRegisterC352[chipId] (already a public field, mirroring
// Audio.GetC352Register) and chipRegister.readC352(chipId) (already public, forwards to
// mds.ReadC352Flag, mirroring Audio.GetC352KeyOn) directly - no new getter needed, like C140.
//
// Clock handling: frmC352.cs's private searchC352Note divides by Audio.ClockC352, a dynamic
// per-format static property (default 24192000 in Audio.cs). This port substitutes the
// clockHz constructor parameter instead, falling back to 24192000 when clockHz is 0, following
// the same pattern used for every other chip whose original code read a dynamic Audio.ClockXxx
// property (SN76489/S5B/AY8910/K051649/C140).
//
// Preserved quirk: exactly like C140's searchC140Note, this chip's searchC352Note keeps
// overwriting (m, n) on every iteration where freq > a instead of finding a global minimum
// distance - the local "m" is assigned but never compared against. Ported verbatim.
//
// Preserved quirk: note is computed unconditionally at the top of the per-channel loop (even
// when c352key is null, which can't actually happen here since readC352 never returns null,
// but the original's null-guard shape is preserved as-is), then only the volumes/pan/mask
// handling and note-reset-to--1 logic live inside the c352key != null block, exactly matching
// frmC352.cs's control flow.
//
// Deliberate simplification: `tp` hardcoded 0 (frmC352.cs derives it from a commented-out/
// disabled "UseScci" setting - `bool C352Type = false;` unconditionally in the original's own
// screenInit), same as every other DrawBuffXxx.cs in this port. screenInit's placeholder
// pre-draw loop is skipped, same as every other chip in this port - the first real
// ScreenDrawParams call naturally draws everything via the normal dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class C352Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly uint clockHz;
        private readonly int ChipID;

        private readonly MDChipParams.C352 newParam = new();
        private readonly MDChipParams.C352 oldParam = new();

        public PixelScreen Screen => screen;

        public C352Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffC352.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeC352");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmC352.cs:147 searchC352Note.
        private int SearchC352Note(int freq)
        {
            double m = double.MaxValue;

            int clock = (int)(clockHz != 0 ? clockHz : 24192000);

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(
                    0x10000
                    * 8000.0
                    * Tables.pcmMulTbl[i % 12 + 12]
                    * System.Math.Pow(2, i / 12 - 3 + 2)
                    / clock
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

        // frmC352.cs:173 screenChangeParams.
        public void ScreenChangeParams()
        {
            ushort[] c352Register = chipRegister.pcmRegisterC352[ChipID];
            ushort[] c352key = chipRegister.readC352((byte)ChipID);
            if (c352Register == null) return;

            for (int ch = 0; ch < 32; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];
                channel.note = SearchC352Note(c352Register[ch * 8 + 2]);

                if (c352key != null)
                {
                    channel.pan = ((c352Register[ch * 8 + 0] >> 12) & 0xf)
                        | ((((ushort)c352Register[ch * 8 + 0] & 0xff) >> 4 & 0xf) << 4);

                    if ((c352Register[ch * 8 + 3] & 0x4000) != 0 && (c352key[ch] & 0x8000) != 0)
                    {
                        channel.volumeL = Common.Range((int)(((ushort)c352Register[ch * 8 + 0] >> 8) / 11.7), 0, 19);
                        channel.volumeR = Common.Range((int)(((ushort)c352Register[ch * 8 + 0] & 0xff) / 11.7), 0, 19);
                        channel.volumeRL = Common.Range((int)(((ushort)c352Register[ch * 8 + 1] >> 8) / 11.7), 0, 19);
                        channel.volumeRR = Common.Range((int)(((ushort)c352Register[ch * 8 + 1] & 0xff) / 11.7), 0, 19);
                    }

                    if (channel.mask == null || channel.mask == true)
                    {
                        channel.pan = 0;
                    }

                    if ((c352key[ch] & 0x8000) == 0)
                    {
                        channel.note = -1;

                        c352Register[ch * 8 + 3] = (ushort)(c352Register[ch * 8 + 3] & 0xbfff);
                        if (channel.volumeL > 0) channel.volumeL--;
                        if (channel.volumeR > 0) channel.volumeR--;
                        if (channel.volumeRL > 0) channel.volumeRL--;
                        if (channel.volumeRR > 0) channel.volumeRR--;
                    }
                }

                int d = c352Register[ch * 8 + 3];
                for (int b = 0; b < 16; b++)
                {
                    channel.bit[b] = (d & (0x8000 >> b)) != 0;
                }

                channel.freq = c352Register[ch * 8 + 2];
                channel.bank = c352Register[ch * 8 + 4];
                channel.sadr = c352Register[ch * 8 + 5];
                channel.eadr = c352Register[ch * 8 + 6];
                channel.ladr = c352Register[ch * 8 + 7];
            }
        }

        // frmC352.cs:236 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 32; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffC352.VolumeXY(screen, 105, ch * 2 + 2, 1, ref oyc.volumeL, nyc.volumeL); // Front
                DrawBuffC352.VolumeXY(screen, 105, ch * 2 + 3, 1, ref oyc.volumeR, nyc.volumeR); // Front
                DrawBuffC352.VolumeXY(screen, 115, ch * 2 + 2, 1, ref oyc.volumeRL, nyc.volumeRL); // Rear
                DrawBuffC352.VolumeXY(screen, 115, ch * 2 + 3, 1, ref oyc.volumeRR, nyc.volumeRR); // Rear

                for (int b = 0; b < 16; b++)
                {
                    DrawBuffC352.DrawNesSw(screen, 64 * 4 + b * 4, ch * 8 + 8, ref oyc.bit[b], nyc.bit[b]);
                }

                DrawBuffC352.Font4Hex16Bit(screen, 4 * 81, ch * 8 + 8, ref oyc.freq, nyc.freq);
                DrawBuffC352.Font4Hex16Bit(screen, 4 * 86, ch * 8 + 8, ref oyc.bank, nyc.bank);
                DrawBuffC352.Font4Hex16Bit(screen, 4 * 91, ch * 8 + 8, ref oyc.sadr, nyc.sadr);
                DrawBuffC352.Font4Hex16Bit(screen, 4 * 96, ch * 8 + 8, ref oyc.eadr, nyc.eadr);
                DrawBuffC352.Font4Hex16Bit(screen, 4 * 101, ch * 8 + 8, ref oyc.ladr, nyc.ladr);
                DrawBuffC352.KeyBoardToC352(screen, ch, ref oyc.note, nyc.note);
                DrawBuffC352.ChC352(screen, ch, ref oyc.mask, nyc.mask);
                DrawBuffC352.PanType2(screen, ch, ref oyc.pan, nyc.pan);
            }

            screen.Present();
        }
    }
}
