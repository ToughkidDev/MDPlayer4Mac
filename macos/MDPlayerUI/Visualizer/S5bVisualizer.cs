// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmS5B.cs - the S5B (Sunsoft FME-7/5B, the
// AY-3-8910-compatible NES/Famicom expansion sound chip used by some cartridge mappers,
// e.g. Gimmick!) channel visualizer: 3 tone/noise channel rows (LED volume bar, piano
// keyboard, tone/noise mode icon) plus the chip-wide hardware envelope generator's
// Frequency/Type readouts. Simpler than AY8910's window - no per-channel decimal volume
// text, no tone-period hex readout, no envelope-mode flag icon (frmS5B.cs's
// screenDrawParams never draws those for this chip).
//
// Data source - genuinely different from every other chip visualizer in this port: every
// other chip's original `Audio.GetXxxRegister` is a thin pass-through to a `ChipRegister`
// field this port already exposes directly. S5B is the one exception - the original's
// `Audio.GetS5BRegister` (Audio.cs:12237) doesn't read a captured write-history array; it
// live-polls the FME-7 emulation core's internal register state via `chipRegister.nes_fme7.
// Read(addr, ref value)` for each of the chip's 0x20 registers, every frame. This port
// mirrors that same live-poll loop directly against `ChipRegister.nes_fme7` (present when
// the loaded file is an NSF using the FME-7 expansion chip, null otherwise) rather than
// against a `fmRegisterXxx`-style array, since no such array exists for this chip on either
// platform.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class S5bVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.S5B newParam = new();
        private readonly MDChipParams.S5B oldParam = new();
        private readonly uint[] s5bRegs = new uint[0x20];

        public PixelScreen Screen => screen;

        public S5bVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffS5b.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeS5B");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // Audio.cs:12237 GetS5BRegister, inlined - live-polls the FME-7 core's registers
        // (see file header). chipID 1 (second chip instance) never has an S5B in the
        // original either - FME-7 is a single NES cartridge expansion chip, not something
        // the format supports pairing two of.
        private bool ReadRegisters()
        {
            if (chipRegister.nes_fme7 == null || ChipID == 1) return false;

            for (uint adr = 0x00; adr < 0x20; adr++)
            {
                uint dat = 0;
                chipRegister.nes_fme7.Read(adr, ref dat);
                s5bRegs[adr] = dat;
            }

            return true;
        }

        // frmS5B.cs:86 screenChangeParams.
        public void ScreenChangeParams()
        {
            if (!ReadRegisters()) return;

            for (int ch = 0; ch < 3; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];

                bool t = (s5bRegs[0x07] & (0x1u << ch)) == 0;
                bool n = (s5bRegs[0x07] & (0x8u << ch)) == 0;
                channel.tn = (t ? 1 : 0) + (n ? 2 : 0);
                newParam.nfrq = (int)(s5bRegs[0x06] & 0x1f);
                newParam.efrq = (int)(s5bRegs[0x0c] * 0x100 + s5bRegs[0x0b]);
                newParam.etype = (int)(s5bRegs[0x0d] & 0xf);

                int v = (int)(s5bRegs[0x08 + ch] & 0x1f);
                v = v > 15 ? 15 : v;
                channel.volume = (int)((t || n ? 1 : 0) * v * (20.0 / 16.0));
                if (!t && !n && channel.volume > 0)
                {
                    channel.volume--;
                }

                if (channel.volume == 0)
                {
                    channel.note = -1;
                }
                else
                {
                    int ft = (int)s5bRegs[0x00 + ch * 2];
                    int ct = (int)s5bRegs[0x01 + ch * 2];
                    int tp = (ct << 8) | ft;
                    if (tp == 0) tp = 1;
                    float clock = clockHz != 0 ? clockHz : 1789772f;
                    float ftone = clock / (8.0f * tp);
                    channel.note = Common.searchSSGNote(ftone);
                }
            }
        }

        // frmS5B.cs:128 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffS5b.Volume(screen, 256, 8 + c * 8, 0, ref oyc.volume, nyc.volume);
                DrawBuffS5b.KeyBoard(screen, c, ref oyc.note, nyc.note);
                DrawBuffS5b.ToneNoise(screen, 6, 2, c, ref oyc.tn, nyc.tn, ref oyc.tntp, 0);

                DrawBuffS5b.ChS5b(screen, c, ref oyc.mask, nyc.mask);
            }

            DrawBuffS5b.Nfrq(screen, 5, 8, ref oldParam.nfrq, newParam.nfrq);
            DrawBuffS5b.Efrq(screen, 18, 8, ref oldParam.efrq, newParam.efrq);
            DrawBuffS5b.Etype(screen, 33, 8, ref oldParam.etype, newParam.etype);

            screen.Present();
        }
    }
}
