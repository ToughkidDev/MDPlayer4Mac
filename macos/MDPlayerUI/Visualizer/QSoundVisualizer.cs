// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmQSound.cs - the Capcom QSound (CQ-SPX67610,
// used across many CPS1/CPS2-era arcade boards) visualizer. 19 channels - 16 PCM voices plus
// 3 ADPCM voices - one 8px row per channel: echo/frequency/bank/sample-start/sample-end/
// loop-start 16-bit hex readouts (PCM channels only), a byte-packed 2-tile pan icon (PCM
// channels only), independent L/R volume LED bars (all 19 channels), a keyboard/note readout
// (PCM channels only), and a channel badge (all 19). Rows 17-18 additionally show the chip's
// shared echo/reverb unit state in unused columns of the ADPCM channel rows.
//
// Data source: reads chipRegister.getQSoundRegister(chipId) directly - already a public
// ChipRegister method (mirrors Audio.GetQSoundRegister), no new getter needed.
//
// Note computation reuses Common.searchSegaPCMNote directly (not duplicated) - the original
// itself calls this shared helper, matching how many other already-ported chips
// (YM2413/YM3526/YM3812/Y8950/YMF262/YMF278B/VRC7) do the same.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class QSoundVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.QSound newParam = new();
        private readonly MDChipParams.QSound oldParam = new();

        public PixelScreen Screen => screen;

        public QSoundVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmQSound.cs's screenChangeParams never reads a clock value.

            DrawBuffQSound.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeQSound");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmQSound.cs:139 screenChangeParams.
        public void ScreenChangeParams()
        {
            ushort[] qSoundRegister = chipRegister.getQSoundRegister(ChipID);
            if (qSoundRegister == null) return;

            // PCM 16ch
            for (int ch = 0; ch < 16; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];

                nyc.echo = qSoundRegister[ch + 0xba];
                nyc.freq = qSoundRegister[(ch << 3) + 2];
                nyc.bank = qSoundRegister[(((ch + 15) % 16) << 3) + 0];
                nyc.sadr = qSoundRegister[(ch << 3) + 1];
                nyc.eadr = qSoundRegister[(ch << 3) + 5];
                nyc.ladr = qSoundRegister[(ch << 3) + 4];
                int vol = qSoundRegister[(ch << 3) + 6];
                int pan = qSoundRegister[ch + 0x80] - 0x110;
                if (pan >= 97) pan = 16; // center?
                int panL = (int)(15.0 / 16.0 * (pan > 16 ? (16 - (33 - pan)) : 16));
                int panR = (int)(15.0 / 16.0 * (pan < 16 ? (16 - pan) : 16));
                nyc.pan = (panR << 4) | panL;
                nyc.volumeL = System.Math.Min(System.Math.Max(vol * panL / 256 / 16, 0), 19);
                nyc.volumeR = System.Math.Min(System.Math.Max(vol * panR / 256 / 16, 0), 19);

                nyc.note = System.Math.Max(System.Math.Min(Common.searchSegaPCMNote(nyc.freq / 16.0 / 166.0), 7 * 12), 0);
                if (vol == 0) nyc.note = -1;
            }

            // ADPCM 3ch
            for (int ch = 0; ch < 3; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch + 16];

                nyc.bank = qSoundRegister[(ch << 2) + 0xcc];
                nyc.sadr = qSoundRegister[(ch << 2) + 0xca];
                nyc.eadr = qSoundRegister[(ch << 2) + 0xcb];
                int vol = qSoundRegister[(ch << 2) + 0xcd] >> 16;
                int pan = qSoundRegister[ch + 16 + 0x80] - 0x110;
                if (pan >= 97) pan = 16; // center?
                int panL = (int)(15.0 / 16.0 * (pan > 16 ? (16 - (33 - pan)) : 16));
                int panR = (int)(15.0 / 16.0 * (pan < 16 ? (16 - pan) : 16));
                nyc.pan = (panR << 4) | panL;
                nyc.volumeL = System.Math.Min(System.Math.Max(vol * panL / 10, 0), 19);
                nyc.volumeR = System.Math.Min(System.Math.Max(vol * panR / 10, 0), 19);
            }

            // echo
            newParam.channels[0].inst[0] = qSoundRegister[0x93]; // feedback
            newParam.channels[0].inst[1] = qSoundRegister[0xd9]; // end_pos
            newParam.channels[0].inst[2] = qSoundRegister[0xe2]; // delay_update
            newParam.channels[0].inst[3] = qSoundRegister[0xe3]; // next_state
            // Wet
            newParam.channels[0].inst[4] = qSoundRegister[0xde]; // delay left
            newParam.channels[0].inst[5] = qSoundRegister[0xe0]; // delay right
            newParam.channels[0].inst[6] = qSoundRegister[0xe4]; // volume_left
            newParam.channels[0].inst[7] = qSoundRegister[0xe6]; // volume right
            // Dry
            newParam.channels[0].inst[8] = qSoundRegister[0xdf]; // delay left
            newParam.channels[0].inst[9] = qSoundRegister[0xe1]; // delay right
            newParam.channels[0].inst[10] = qSoundRegister[0xe5]; // volume_left
            newParam.channels[0].inst[11] = qSoundRegister[0xe7]; // volume right
        }

        // frmQSound.cs:198 screenDrawParams.
        public void ScreenDrawParams()
        {
            // PCM 16ch
            for (int ch = 0; ch < 16; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 65, ch * 8 + 8, ref oyc.echo, nyc.echo);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 70, ch * 8 + 8, ref oyc.freq, nyc.freq);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 75, ch * 8 + 8, ref oyc.bank, nyc.bank);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 80, ch * 8 + 8, ref oyc.sadr, nyc.sadr);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 85, ch * 8 + 8, ref oyc.eadr, nyc.eadr);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 90, ch * 8 + 8, ref oyc.ladr, nyc.ladr);
                DrawBuffQSound.PanType2(screen, ch, ref oyc.pan, nyc.pan);
                DrawBuffQSound.VolumeXY(screen, 94, ch * 2 + 2, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffQSound.VolumeXY(screen, 94, ch * 2 + 3, 1, ref oyc.volumeR, nyc.volumeR);
                DrawBuffQSound.KeyBoardToQSound(screen, ch, ref oyc.note, nyc.note);

                DrawBuffQSound.ChQSound(screen, ch, ref oyc.mask, nyc.mask);
            }

            // ADPCM 3ch
            for (int ch = 0; ch < 3; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch + 16];
                MDChipParams.Channel nyc = newParam.channels[ch + 16];

                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 75, (ch + 16) * 8 + 8, ref oyc.bank, nyc.bank);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 80, (ch + 16) * 8 + 8, ref oyc.sadr, nyc.sadr);
                DrawBuffQSound.Font4Hex16Bit(screen, 4 * 85, (ch + 16) * 8 + 8, ref oyc.eadr, nyc.eadr);
                DrawBuffQSound.VolumeXY(screen, 94, (ch + 16) * 2 + 2, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffQSound.VolumeXY(screen, 94, (ch + 16) * 2 + 3, 1, ref oyc.volumeR, nyc.volumeR);

                DrawBuffQSound.ChQSound(screen, ch + 16, ref oyc.mask, nyc.mask);
            }

            // echo
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 36, 17 * 8 + 8, ref oldParam.channels[0].inst[0], newParam.channels[0].inst[0]); // feedback
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 36, 18 * 8 + 8, ref oldParam.channels[0].inst[1], newParam.channels[0].inst[1]); // end_pos
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 51, 17 * 8 + 8, ref oldParam.channels[0].inst[2], newParam.channels[0].inst[2]); // delay_update
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 51, 18 * 8 + 8, ref oldParam.channels[0].inst[3], newParam.channels[0].inst[3]); // next_state

            // Wet
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 7, 17 * 8 + 8, ref oldParam.channels[0].inst[4], newParam.channels[0].inst[4]); // delay l
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 12, 17 * 8 + 8, ref oldParam.channels[0].inst[5], newParam.channels[0].inst[5]); // delay r
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 7, 18 * 8 + 8, ref oldParam.channels[0].inst[6], newParam.channels[0].inst[6]); // vol l
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 12, 18 * 8 + 8, ref oldParam.channels[0].inst[7], newParam.channels[0].inst[7]); // vol r

            // Dry
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 18, 17 * 8 + 8, ref oldParam.channels[0].inst[8], newParam.channels[0].inst[8]); // delay l
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 23, 17 * 8 + 8, ref oldParam.channels[0].inst[9], newParam.channels[0].inst[9]); // delay r
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 18, 18 * 8 + 8, ref oldParam.channels[0].inst[10], newParam.channels[0].inst[10]); // vol l
            DrawBuffQSound.Font4Hex16Bit(screen, 4 * 23, 18 * 8 + 8, ref oldParam.channels[0].inst[11], newParam.channels[0].inst[11]); // vol r

            screen.Present();
        }
    }
}
