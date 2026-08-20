// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmMultiPCM.cs - the Yamaha YMW258-F (MultiPCM)
// 28-channel wavetable/PCM sample player visualizer, used by many Sega arcade and console
// boards. One 8px row per channel across 28 channels, each showing a byte-packed 2-tile pan
// icon, a "key on" flag icon, an instrument number, a 12-bit frequency readout, a "TL
// interpolation" flag icon, TL/LFO-freq/PLFO/ALFO readouts, 24-bit sample-start / 16-bit
// sample-end / 16-bit sample-loop address readouts, LFO vibrato/attack/decay1/decay2/decay-
// level/release/key-rate-scale/AM hex readouts, independent L/R volume LED bars, and a
// keyboard/note readout.
//
// Data source: reads chipRegister.getMultiPCMRegister(chipId) directly - already a public
// ChipRegister method (mirrors Audio.GetMultiPCMRegister), no new getter needed.
//
// No clock handling needed - frmMultiPCM.cs's screenChangeParams never reads a clock value
// (the commented-out searchMultiPCMNote is the only place a clock would have been used, and
// it's entirely dead code in the original, preserved as dead code here too - see below).
//
// Genuinely-preserved original quirks (not "fixed"):
// - screenDrawParams never calls ChMultiPCM/ChMultiPCM_P - no channel badge is ever drawn for
//   this chip (the call is commented out in the original source).
// - searchMultiPCMNote is entirely commented-out dead code in the original (always returns 0)
//   and is genuinely unused - screenChangeParams computes its note a completely different way
//   (from the octave/pitch registers directly, not via a search table) - so this port omits
//   the dead function rather than porting a no-op.
// - KeyBoardToMultiPCM's octave bound-check is `< 8`, not the `< 10` used by most other
//   chips' KeyBoard* overloads, since this chip's note range is clamped to 7 octaves max.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class MultiPCMVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.MultiPCM newParam = new();
        private readonly MDChipParams.MultiPCM oldParam = new();

        public PixelScreen Screen => screen;

        public MultiPCMVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmMultiPCM.cs's screenChangeParams never reads a clock value.

            DrawBuffMultiPCM.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeMultiPCM");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmMultiPCM.cs:172 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.multipcm._MultiPCM multiPcmRegister = chipRegister.getMultiPCMRegister(ChipID);
            if (multiPcmRegister == null) return;

            for (int ch = 0; ch < 28; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                MDSound.multipcm._slot_t slot = multiPcmRegister.slots[ch];

                int oct = ((slot.regs[3] >> 4) - 1) & 0xf;
                oct = ((oct & 0x8) != 0) ? (oct - 16) : oct;
                oct = oct + 4; // 基音を o5 にしてます
                int pitch = ((slot.regs[3] & 0xf) << 6) | (slot.regs[2] >> 2);

                int nt = System.Math.Max(System.Math.Min(oct * 12 + pitch / 85, 7 * 12), 0);
                nyc.note = nt;

                int d = (int)slot.pan;
                d = (d == 0) ? 0xf : d;
                nyc.pan = ((((d & 0xc) >> 2) * 4) << 4) | (((d & 0x3) * 4) << 0);

                nyc.bit[0] = (slot.regs[4] & 0x80) != 0;
                nyc.freq = ((slot.regs[3] & 0xf) << 6) | (slot.regs[2] >> 2);
                nyc.bit[1] = (slot.regs[5] & 1) != 0; // TL Interpolation
                nyc.inst[1] = (slot.regs[5] >> 1) & 0x7f; // TL
                nyc.inst[2] = (slot.regs[6] >> 3) & 7; // LFO freq
                nyc.inst[3] = (slot.regs[6]) & 7; // PLFO
                nyc.inst[4] = (slot.regs[7]) & 7; // ALFO

                if (slot.sample != null)
                {
                    nyc.inst[0] = slot.regs[1];
                    nyc.sadr = (int)slot.sample.start;
                    nyc.eadr = (int)slot.sample.end;
                    nyc.ladr = (int)slot.sample.loop;
                    nyc.inst[5] = slot.sample.lfo_vibrato_reg;
                    nyc.inst[6] = slot.sample.attack_reg;
                    nyc.inst[7] = slot.sample.decay1_reg;
                    nyc.inst[8] = slot.sample.decay2_reg;
                    nyc.inst[9] = slot.sample.decay_level;
                    nyc.inst[10] = slot.sample.release_reg;
                    nyc.inst[11] = slot.sample.key_rate_scale;
                    nyc.inst[12] = slot.sample.lfo_amplitude_reg;
                }

                if (nyc.bit[0])
                {
                    nyc.volumeL = System.Math.Min((int)(((0x7f - nyc.inst[1]) * ((nyc.pan >> 4) & 0xf) / 0xf) / 4.5), 19);
                    nyc.volumeR = System.Math.Min((int)(((0x7f - nyc.inst[1]) * ((nyc.pan) & 0xf) / 0xf) / 4.5), 19);
                }
                else
                {
                    nyc.note = -1;
                    if (nyc.volumeL > 0) nyc.volumeL--;
                    if (nyc.volumeR > 0) nyc.volumeR--;
                }
            }
        }

        // frmMultiPCM.cs:235 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 28; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffMultiPCM.PanType2(screen, ch, ref oyc.pan, nyc.pan);

                DrawBuffMultiPCM.DrawNesSw(screen, 64 * 4, ch * 8 + 8, ref oldParam.channels[ch].bit[0], newParam.channels[ch].bit[0]);
                DrawBuffMultiPCM.Font4HexByte(screen, 4 * 66, ch * 8 + 8, ref oyc.inst[0], nyc.inst[0]);
                DrawBuffMultiPCM.Font4Hex12Bit(screen, 4 * 69, ch * 8 + 8, ref oyc.freq, nyc.freq);
                DrawBuffMultiPCM.DrawNesSw(screen, 72 * 4, ch * 8 + 8, ref oldParam.channels[ch].bit[1], newParam.channels[ch].bit[1]); // TL Interpolation
                DrawBuffMultiPCM.Font4HexByte(screen, 4 * 74, ch * 8 + 8, ref oyc.inst[1], nyc.inst[1]); // TL
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 77, ch * 8 + 8, ref oyc.inst[2], nyc.inst[2]); // LFO freq
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 79, ch * 8 + 8, ref oyc.inst[3], nyc.inst[3]); // PLFO
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 81, ch * 8 + 8, ref oyc.inst[4], nyc.inst[4]); // ALFO
                DrawBuffMultiPCM.Font4Hex24Bit(screen, 4 * 83, ch * 8 + 8, ref oyc.sadr, nyc.sadr);
                DrawBuffMultiPCM.Font4Hex16Bit(screen, 4 * 90, ch * 8 + 8, ref oyc.eadr, nyc.eadr);
                DrawBuffMultiPCM.Font4Hex16Bit(screen, 4 * 95, ch * 8 + 8, ref oyc.ladr, nyc.ladr);
                DrawBuffMultiPCM.Font4HexByte(screen, 4 * 100, ch * 8 + 8, ref oyc.inst[5], nyc.inst[5]); // LFOVIB
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 103, ch * 8 + 8, ref oyc.inst[6], nyc.inst[6]); // AR
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 105, ch * 8 + 8, ref oyc.inst[7], nyc.inst[7]); // DR1
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 107, ch * 8 + 8, ref oyc.inst[8], nyc.inst[8]); // DR2
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 109, ch * 8 + 8, ref oyc.inst[9], nyc.inst[9]); // DL
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 111, ch * 8 + 8, ref oyc.inst[10], nyc.inst[10]); // RR
                DrawBuffMultiPCM.Font4Hex4Bit(screen, 4 * 113, ch * 8 + 8, ref oyc.inst[11], nyc.inst[11]); // KRS
                DrawBuffMultiPCM.Font4HexByte(screen, 4 * 115, ch * 8 + 8, ref oyc.inst[12], nyc.inst[12]); // AM

                DrawBuffMultiPCM.VolumeXY(screen, 117, ch * 2 + 2, 1, ref oyc.volumeL, nyc.volumeL); // Front L
                DrawBuffMultiPCM.VolumeXY(screen, 117, ch * 2 + 3, 1, ref oyc.volumeR, nyc.volumeR); // Front R

                DrawBuffMultiPCM.KeyBoardToMultiPCM(screen, ch, ref oyc.note, nyc.note);
            }

            screen.Present();
        }
    }
}
