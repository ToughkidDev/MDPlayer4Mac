// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmVRC7.cs - the VRC7 cartridge mapper's
// expansion audio channel visualizer. VRC7 wraps a cut-down YM2413 (OPLL) FM core: 6 melody
// channels, no rhythm section. Each channel reads its own note/instrument-number/sustain/
// volume bits; only channel 0 carries the shared 28-value instrument-parameter table
// (operator envelope/tone settings that apply to whichever instrument register bank is
// currently selected).
//
// Data source: reads chipRegister.GetVRC7Register(chipID) (raw register bytes - a new
// public wrapper added alongside this file, see ChipRegister.cs's comment) and
// chipRegister.getVRC7KeyInfo(chipID) (a ChipKeyInfo one-shot-key-on tracker that already
// existed, same pattern already used by YM3812/Y8950's ports) to detect one-shot key-on
// events between polls and flash the volume meter even if the note has already decayed by
// the time this frame is sampled.
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop is skipped, same as every other chip in this
// port - the first real ScreenDrawParams call naturally draws everything via the normal
// dirty-diff path.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Vrc7Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.VRC7 newParam = new();
        private readonly MDChipParams.VRC7 oldParam = new();

        public PixelScreen Screen => screen;

        public Vrc7Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmVRC7.cs's screenChangeParams never reads a clock value.

            DrawBuffVrc7.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeVRC7");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmVRC7.cs:88 screenChangeParams.
        public void ScreenChangeParams()
        {
            byte[] reg = chipRegister.GetVRC7Register(ChipID);
            if (reg == null) return;

            ChipKeyInfo ki = chipRegister.getVRC7KeyInfo(ChipID);

            for (int ch = 0; ch < 6; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];

                nyc.inst[0] = (reg[0x30 + ch] & 0xf0) >> 4; // Instrument number
                nyc.inst[1] = (reg[0x20 + ch] & 0x20) >> 5; // Sustain
                nyc.inst[2] = (reg[0x20 + ch] & 0x10) >> 4; // Current key-on state
                nyc.inst[3] = reg[0x30 + ch] & 0x0f; // Volume

                int freq = reg[0x10 + ch] + ((reg[0x20 + ch] & 0x1) << 8);
                int oct = (reg[0x20 + ch] & 0xe) >> 1;
                nyc.note = Common.searchSegaPCMNote(freq / 172.0) + (oct - 4) * 12;

                if (ki.On[ch])
                {
                    nyc.volumeL = 19 - nyc.inst[3];
                }
                else
                {
                    if (nyc.inst[2] == 0) nyc.note = -1;

                    nyc.volumeL--;
                    if (nyc.volumeL < 0) nyc.volumeL = 0;
                }
            }

            MDChipParams.Channel ch0 = newParam.channels[0];
            ch0.inst[4] = reg[0x02] & 0x3f; // TL
            ch0.inst[5] = reg[0x03] & 0x07; // FB

            ch0.inst[6] = (reg[0x04] & 0xf0) >> 4; // AR (modulator)
            ch0.inst[7] = reg[0x04] & 0x0f; // DR (modulator)
            ch0.inst[8] = (reg[0x06] & 0xf0) >> 4; // SL (modulator)
            ch0.inst[9] = reg[0x06] & 0x0f; // RR (modulator)
            ch0.inst[10] = (reg[0x02] & 0x80) >> 7; // KL (modulator)
            ch0.inst[11] = reg[0x00] & 0x0f; // MT (modulator)
            ch0.inst[12] = (reg[0x00] & 0x80) >> 7; // AM (modulator)
            ch0.inst[13] = (reg[0x00] & 0x40) >> 6; // VB (modulator)
            ch0.inst[14] = (reg[0x00] & 0x20) >> 5; // EG (modulator)
            ch0.inst[15] = (reg[0x00] & 0x10) >> 4; // KR (modulator)
            ch0.inst[16] = (reg[0x03] & 0x08) >> 3; // DM (modulator)
            ch0.inst[17] = (reg[0x05] & 0xf0) >> 4; // AR (carrier)
            ch0.inst[18] = reg[0x05] & 0x0f; // DR (carrier)
            ch0.inst[19] = (reg[0x07] & 0xf0) >> 4; // SL (carrier)
            ch0.inst[20] = reg[0x07] & 0x0f; // RR (carrier)
            ch0.inst[21] = (reg[0x03] & 0x80) >> 7; // KL (carrier)
            ch0.inst[22] = reg[0x01] & 0x0f; // MT (carrier)
            ch0.inst[23] = (reg[0x01] & 0x80) >> 7; // AM (carrier)
            ch0.inst[24] = (reg[0x01] & 0x40) >> 6; // VB (carrier)
            ch0.inst[25] = (reg[0x01] & 0x20) >> 5; // EG (carrier)
            ch0.inst[26] = (reg[0x01] & 0x10) >> 4; // KR (carrier)
            ch0.inst[27] = (reg[0x03] & 0x10) >> 4; // DC
        }

        // frmVRC7.cs:168 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 6; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffVrc7.Volume(screen, 256, 8 + c * 8, 0, ref oyc.volumeL, nyc.volumeL);
                DrawBuffVrc7.KeyBoard(screen, c, ref oyc.note, nyc.note);

                DrawBuffVrc7.DrawInstNumber(screen, (c % 3) * 16 + 37, (c / 3) * 2 + 16, ref oyc.inst[0], nyc.inst[0]);
                DrawBuffVrc7.SusFlag(screen, (c % 3) * 16 + 41, (c / 3) * 2 + 16, 0, ref oyc.inst[1], nyc.inst[1]);
                DrawBuffVrc7.SusFlag(screen, (c % 3) * 16 + 44, (c / 3) * 2 + 16, 0, ref oyc.inst[2], nyc.inst[2]);
                DrawBuffVrc7.DrawInstNumber(screen, (c % 3) * 16 + 46, (c / 3) * 2 + 16, ref oyc.inst[3], nyc.inst[3]);

                DrawBuffVrc7.ChVrc7(screen, c, ref oyc.mask, nyc.mask);
            }

            MDChipParams.Channel oc0 = oldParam.channels[0];
            MDChipParams.Channel nc0 = newParam.channels[0];
            DrawBuffVrc7.DrawInstNumber(screen, 9, 14, ref oc0.inst[4], nc0.inst[4]); // TL
            DrawBuffVrc7.DrawInstNumber(screen, 14, 14, ref oc0.inst[5], nc0.inst[5]); // FB

            for (int c = 0; c < 11; c++)
            {
                DrawBuffVrc7.DrawInstNumber(screen, c * 3, 18, ref oc0.inst[6 + c], nc0.inst[6 + c]);
                DrawBuffVrc7.DrawInstNumber(screen, c * 3, 20, ref oc0.inst[17 + c], nc0.inst[17 + c]);
            }

            screen.Present();
        }
    }
}
