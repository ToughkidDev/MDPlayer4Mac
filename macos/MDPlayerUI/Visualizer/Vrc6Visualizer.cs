// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmVRC6.cs - the VRC6 cartridge mapper's
// expansion audio channel visualizer. 3 channels: 2 pulse/square (indices 0-1, with
// duty-cycle icon) and 1 sawtooth (index 2) - laid out as 3 uniform 16px-tall rows (taller
// than most chips' 8px rows, but still a regular grid, unlike NESDMC/FDS/MMC5's scattered
// hand-placed layout).
//
// Data source: reads chipRegister.GetVRC6Register(chipID) - already-decoded
// MDSound.np.chip.TrackInfoBasic[] track-info objects, added to ChipRegister.cs alongside
// this file as a one-line forward (see that file's comment) since the underlying
// getVRC6Register(chipID) already existed from a prior session's core-emulation port work.
// Declared TrackInfoBasic[] (matching frmVRC6.cs's own cast), not the base ITrackInfo[] the
// method signature returns - see DrawBuffVrc6.cs's header comment for why that cast matters.
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop (blank piano keys) is skipped, same as every
// other chip in this port - the first real ScreenDrawParams call naturally draws everything
// via the normal dirty-diff path.
using MDPlayer;
using MDSound.np.chip;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Vrc6Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.VRC6 newParam = new();
        private readonly MDChipParams.VRC6 oldParam = new();

        public PixelScreen Screen => screen;

        public Vrc6Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmVRC6.cs's screenChangeParams never reads a clock value.

            DrawBuffVrc6.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeVRC6");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmVRC6.cs:148 screenChangeParams.
        public void ScreenChangeParams()
        {
            TrackInfoBasic[] info = (TrackInfoBasic[])chipRegister.GetVRC6Register(ChipID);
            if (info == null) return;

            for (int ch = 0; ch < 3; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                nyc.kf = info[ch].GetTone();
                nyc.volumeR = info[ch].GetTone() / 4;
                nyc.volumeL = info[ch].GetVolume();
                int v = info[ch].GetVolume();
                v = ch < 2 ? v * 2 : v / 3;
                nyc.volume = System.Math.Min(v, 19);
                nyc.bit[0] = info[ch].GetKeyStatus();
                nyc.freq = info[ch].GetFreqp();
                nyc.bit[1] = info[ch].GetHalt();
                v = info[ch].GetNote(info[ch].GetFreqHz()) - 4 * 12;
                nyc.note = nyc.volumeL == 0 ? -1 : v;
                nyc.sadr = info[ch].GetFreqShift();
            }
        }

        // frmVRC6.cs:174 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int ch = 0; ch < 3; ch++)
            {
                MDChipParams.Channel oyc = oldParam.channels[ch];
                MDChipParams.Channel nyc = newParam.channels[ch];

                DrawBuffVrc6.KeyBoard(screen, ch * 2, ref oyc.note, nyc.note);
                if (ch < 2)
                {
                    DrawBuffVrc6.DrawDuty(screen, 24, (1 + ch * 2) * 8, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffVrc6.Font4Int2(screen, 6 * 4, ch * 16 + 16, ref oyc.kf, nyc.kf);
                    DrawBuffVrc6.Font4Int2(screen, 10 * 4, ch * 16 + 16, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffVrc6.Volume(screen, 256, 8 + ch * 2 * 8, 0, ref oyc.volume, nyc.volume);
                    DrawBuffVrc6.ChVrc6(screen, ch, ref oldParam.channels[ch].mask, newParam.channels[ch].mask);
                }
                else
                {
                    DrawBuffVrc6.Font4Int3(screen, 9 * 4, ch * 16 + 16, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffVrc6.Volume(screen, 256, 8 + ch * 2 * 8, 0, ref oyc.volume, nyc.volume);
                    DrawBuffVrc6.DrawNesSw(screen, 55 * 4, ch * 16 + 16, ref oldParam.channels[ch].bit[1], newParam.channels[ch].bit[1]);
                    DrawBuffVrc6.Font4Int1(screen, 62 * 4, ch * 16 + 16, ref oyc.sadr, nyc.sadr);
                    DrawBuffVrc6.ChVrc6(screen, ch, ref oldParam.channels[ch].mask, newParam.channels[ch].mask);
                }

                DrawBuffVrc6.DrawNesSw(screen, 13 * 4, ch * 16 + 16, ref oldParam.channels[ch].bit[0], newParam.channels[ch].bit[0]);
                DrawBuffVrc6.Font4Hex12Bit(screen, 16 * 4, ch * 16 + 16, ref oyc.freq, nyc.freq);
            }

            screen.Present();
        }
    }
}
