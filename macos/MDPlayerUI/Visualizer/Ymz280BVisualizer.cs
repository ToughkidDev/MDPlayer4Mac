// Port of MDPlayer/MDPlayerx64/form/KB/frmYMZ280B.cs - Yamaha YMZ280B ("PCMD8") 8-channel
// ADPCM sample player. One 8px row per channel, each showing a keyboard/note readout,
// independent L/R volume LED bars, two independent single-tile pan icons (L and R, drawn
// separately rather than as one byte-packed 2-tile icon), 4 on/off flag icons (key-on/EX/
// noise/loop), a raw pan-nibble hex readout, sample start/loop/loop-end/end address hex
// readouts (24-bit each), a pitch readout (12-bit), a TL readout (raw byte), and a channel
// badge. This is the top-level frmYMZ280B.cs, not one of the PCM-subfolder chips - the PCM
// family (C140/C352/GA20/K053260/K054539/MegaCD/MpcmX68k/MultiPCM/OKIM6258/OKIM6295/PCM8/
// QSound/Rf5c68/SegaPCM) finished immediately before this chip.
//
// Data source: reads chipRegister.YMZ280BRegister[chipId] directly - already a public
// ChipRegister field (Audio.GetYMZ280BRegister just forwards to the same field on Windows),
// so no new getter was needed, same shape as C140/SegaPCM's direct-field-access chips.
//
// Note computation: unlike every PCM-family chip so far, this one does NOT reuse
// Common.searchSegaPCMNote or any other shared Common.searchXxxNote helper - the original
// defines its own private 96-entry NoteTableOct frequency-boundary table (credited "Furnace"
// in a source comment, i.e. ported from the Furnace tracker's YMZ280B emulation) and a
// distinct SearchNote that finds the largest table index whose boundary value is <= freq
// (returns i-1 at the first entry where freq < table[i], or the last index if freq exceeds
// every entry) - a different search shape from every other chip's nearest-neighbour or
// global-minimum searches. Ported verbatim, including the table.
//
// No clock handling needed - SearchNote compares the raw 9-bit frequency register field
// directly against the note table, with no Audio.ClockXxx division anywhere in the original.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ymz280BVisualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly int ChipID;

        private readonly MDChipParams.YMZ280B newParam = new();
        private readonly MDChipParams.YMZ280B oldParam = new();

        public PixelScreen Screen => screen;

        public Ymz280BVisualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            _ = clockHz; // frmYMZ280B.cs's screenChangeParams never reads a clock value.

            DrawBuffYmz280B.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYMZ280B");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();

            // frmYMZ280B.cs:129 screenInit - primes panL/panR to their "centre" tile index (4)
            // so the first ScreenDrawParams call doesn't draw a spurious transition from 0.
            for (int ch = 0; ch < 8; ch++)
            {
                newParam.channels[ch].volumeL = 0;
                newParam.channels[ch].volumeR = 0;
                newParam.channels[ch].panL = 4;
                newParam.channels[ch].panR = 4;
                oldParam.channels[ch].panL = -1;
                oldParam.channels[ch].panR = -1;
            }
        }

        // frmYMZ280B.cs:239 NoteTableOct ("Furnace"-credited frequency-boundary table).
        private static readonly int[] NoteTableOct = new int[]
        {
            //Oct0
            0x002, 0x002, 0x002, 0x002, 0x002, 0x003, 0x003, 0x003, 0x004, 0x004, 0x004, 0x004,
            //Oct1
            0x005, 0x005, 0x006, 0x006, 0x006, 0x007, 0x007, 0x008, 0x008, 0x009, 0x009, 0x00a,
            //Oct2
            0x00b, 0x00b, 0x00c, 0x00d, 0x00e, 0x00f, 0x00f, 0x010, 0x011, 0x013, 0x014, 0x015,
            //Oct3
            0x016, 0x018, 0x019, 0x01b, 0x01c, 0x01e, 0x020, 0x022, 0x024, 0x026, 0x028, 0x02b,
            //Oct4
            0x02d, 0x030, 0x033, 0x036, 0x03a, 0x03d, 0x041, 0x045, 0x049, 0x04d, 0x052, 0x057,
            //Oct5
            0x05c, 0x062, 0x067, 0x06e, 0x074, 0x07b, 0x082, 0x08a, 0x093, 0x09b, 0x0a5, 0x0af,
            //Oct6
            0x0b9, 0x0c4, 0x0d0, 0x0dc, 0x0e9, 0x0f7, 0x106, 0x116, 0x126, 0x138, 0x14A, 0x15E,
            //Oct7
            0x173, 0x189, 0x1A0, 0x1B9, 0x1D4, 0x1EF, 0x1ff, 0x1ff, 0x1ff, 0x1ff, 0x1ff, 0x1ff
        };

        // frmYMZ280B.cs:259 SearchNote.
        private static int SearchNote(int freq)
        {
            for (int i = 0; i < NoteTableOct.Length; i++)
            {
                if (freq < NoteTableOct[i]) return i - 1;
            }
            return NoteTableOct.Length - 1;
        }

        // frmYMZ280B.cs:154 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] reg = chipRegister.YMZ280BRegister[ChipID];
            if (reg == null) return;

            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel nrc = newParam.channels[ch];

                nrc.freq = (byte)reg[0x0 + ch * 4]
                    + ((reg[0x1 + ch * 4] & 1) << 8);
                nrc.nfrq = (byte)reg[0x2 + ch * 4]; // tl

                nrc.pan = (byte)(reg[0x3 + ch * 4] & 0xf);
                nrc.panL = nrc.pan == 8 ? 4 : (nrc.pan < 8 ? 4 : (4 * (15 - nrc.pan) / 7));
                nrc.panR = nrc.pan == 8 ? 4 : (nrc.pan < 8 ? ((nrc.pan == 0) ? 0 : 4 * (nrc.pan - 1) / 7) : 4);

                nrc.sadr = ((byte)reg[0x20 + ch * 4] << 16)
                    + ((byte)reg[0x40 + ch * 4] << 8)
                    + (byte)reg[0x60 + ch * 4];
                nrc.ladr = ((byte)reg[0x21 + ch * 4] << 16)
                    + ((byte)reg[0x41 + ch * 4] << 8)
                    + (byte)reg[0x61 + ch * 4];
                nrc.leadr = ((byte)reg[0x22 + ch * 4] << 16)
                    + ((byte)reg[0x42 + ch * 4] << 8)
                    + (byte)reg[0x62 + ch * 4];
                nrc.eadr = ((byte)reg[0x23 + ch * 4] << 16)
                    + ((byte)reg[0x43 + ch * 4] << 8)
                    + (byte)reg[0x63 + ch * 4];

                nrc.dda = (reg[0x1 + ch * 4] & 0x80) != 0; // key on
                nrc.ex = (reg[0x1 + ch * 4] & 0x40) != 0;
                nrc.noise = (reg[0x1 + ch * 4] & 0x20) != 0;
                nrc.loopFlg = (reg[0x1 + ch * 4] & 0x10) != 0;

                int vol = System.Math.Min(19, nrc.nfrq / 12);
                nrc.note = -1;
                if (nrc.dda)
                {
                    if (vol > 0)
                    {
                        nrc.note = SearchNote(nrc.freq);
                    }
                    nrc.volumeL = nrc.pan == 8 ? vol : (nrc.pan < 8 ? vol : (vol * (15 - nrc.pan) / 7));
                    nrc.volumeR = nrc.pan == 8 ? vol : (nrc.pan < 8 ? ((nrc.pan == 0) ? 0 : vol * (nrc.pan - 1) / 7) : 4);
                }
                else
                {
                    nrc.volumeL += nrc.volumeL > 0 ? -1 : 0;
                    nrc.volumeR += nrc.volumeR > 0 ? -1 : 0;
                }
            }
        }

        // frmYMZ280B.cs:210 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel orc = oldParam.channels[ch];
                MDChipParams.Channel nrc = newParam.channels[ch];

                DrawBuffYmz280B.ChYmz280B(screen, ch, ref orc.mask, nrc.mask);
                DrawBuffYmz280B.PanType5(screen, 6 * 4 + 1, ch * 8 + 8, ref orc.panL, nrc.panL);
                DrawBuffYmz280B.PanType5(screen, 7 * 4 + 1, ch * 8 + 8, ref orc.panR, nrc.panR);
                DrawBuffYmz280B.KeyBoardYMZ280B(screen, 4 * 8 + 1, ch * 8 + 8, ref orc.note, nrc.note);
                DrawBuffYmz280B.Volume(screen, 68 * 4 + 1, 8 + ch * 8, 1, ref orc.volumeL, nrc.volumeL);
                DrawBuffYmz280B.Volume(screen, 68 * 4 + 1, 8 + ch * 8, 2, ref orc.volumeR, nrc.volumeR);

                DrawBuffYmz280B.Font4Hex4Bit(screen, 4 * 8 + 1, ch * 8 + 10 * 8, ref orc.pan, nrc.pan);
                DrawBuffYmz280B.DrawNesSw(screen, 4 * 64 + 1, ch * 8 + 8, ref orc.dda, nrc.dda); // KEY ON
                DrawBuffYmz280B.DrawNesSw(screen, 4 * 65 + 1, ch * 8 + 8, ref orc.ex, nrc.ex);
                DrawBuffYmz280B.DrawNesSw(screen, 4 * 66 + 1, ch * 8 + 8, ref orc.noise, nrc.noise);
                DrawBuffYmz280B.DrawNesSw(screen, 4 * 67 + 1, ch * 8 + 8, ref orc.loopFlg, nrc.loopFlg);
                DrawBuffYmz280B.Font4Hex24Bit(screen, 4 * 13 + 1, ch * 8 + 10 * 8, ref orc.sadr, nrc.sadr);
                DrawBuffYmz280B.Font4Hex24Bit(screen, 4 * 22 + 1, ch * 8 + 10 * 8, ref orc.ladr, nrc.ladr);
                DrawBuffYmz280B.Font4Hex24Bit(screen, 4 * 31 + 1, ch * 8 + 10 * 8, ref orc.leadr, nrc.leadr);
                DrawBuffYmz280B.Font4Hex24Bit(screen, 4 * 40 + 1, ch * 8 + 10 * 8, ref orc.eadr, nrc.eadr);
                DrawBuffYmz280B.Font4Hex12Bit(screen, 4 * 49 + 1, ch * 8 + 10 * 8, ref orc.freq, nrc.freq); // PITCH
                DrawBuffYmz280B.Font4HexByte(screen, 4 * 55 + 1, ch * 8 + 10 * 8, ref orc.nfrq, nrc.nfrq); // TL
            }

            screen.Present();
        }
    }
}
