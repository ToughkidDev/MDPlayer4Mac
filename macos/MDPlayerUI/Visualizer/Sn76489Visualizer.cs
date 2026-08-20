// Port of MDPlayer/MDPlayerx64/form/KB/PSG/frmSN76489.cs - the SN76489 (PSG) channel
// visualizer: a 4-row LED volume-meter + piano-key + pan display (3 tone channels + 1 noise
// channel). Only the primary (non-NGP) ScreenChangeParams/ScreenDrawParams path is ported -
// frmSN76489.cs's NGP (Neo Geo Pocket dual-chip) branch specifically is out of scope. Regular
// VGM dual-chip playback (SN76489DualChipFlag) IS supported though: MainWindow.axaml.cs
// constructs a second instance of this class with chipID: 1 when that flag is set (see
// VgmEngine.cs, which now wires up both chip instances into MDSound/ChipRegister).
//
// Data source: the original reads through Audio.GetPSGRegister/GetPSGVolume/
// GetPSGRegisterGGPanning/ClockSN76489 (Audio.cs, itself deliberately not ported - see
// AudioShim.cs). Those are thin wrappers around fields directly on ChipRegister, which IS
// ported near-1:1 (see macos/README.md), so this reads chipRegister.sn76489Register/
// sn76489RegisterGGPan/GetPSGVolume() directly instead, plus MusicEngineSession.ChipClocks
// for the clock value (added alongside this visualizer specifically to give it a home - see
// VgmEngine.cs/MusicEngine.cs).
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Sn76489Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private readonly int ChipID;

        private readonly MDChipParams.SN76489 newParam = new();
        private readonly MDChipParams.SN76489 oldParam = new();

        public PixelScreen Screen => screen;

        public Sn76489Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffSn76489.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeSN76489");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            // Draw the static background (labels, table borders, default-state keyboard
            // grid, default "MODE: PERIODIC RATE: CH3" text) once - every later frame only
            // blits small opaque sprites on top of this, never redraws it (see
            // PixelScreen.cs's header comment for why that's sufficient here).
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);

            // frmSN76489.ScreenInit(): the one dirty-diff baseline that doesn't already
            // match MDChipParams.Channel's own field defaults (note=-1 everywhere else) -
            // channel 3's `note` field is reused to carry the raw noise-control register
            // byte (see DrawBuffSn76489.ChSN76489Noise), whose power-on-reset value is 0,
            // not -1. Without this, the first ChSN76489Noise call would spuriously treat
            // "0" as different from the Channel() default "-1" and redraw text that's
            // already correct in the background art - harmless, but this matches the
            // original exactly rather than relying on that being harmless.
            oldParam.channels[3].note = 0;

            screen.Present();
        }

        // frmSN76489.cs:93 ScreenChangeParams (primary/non-NGP path only).
        public void ScreenChangeParams()
        {
            int[]? psgRegister = chipRegister.sn76489Register[ChipID];
            int psgRegisterPan = chipRegister.sn76489RegisterGGPan[ChipID];
            int[][] psgVol = chipRegister.GetPSGVolume(ChipID);

            if (psgRegister == null) return;

            for (int ch = 0; ch < 3; ch++)
            {
                newParam.channels[ch].freq = psgRegister[ch * 2];
                if (psgRegister[ch * 2 + 1] != 15 && newParam.channels[ch].freq != 0)
                {
                    float ftone = clockHz / (2.0f * psgRegister[ch * 2] * 16.0f);
                    newParam.channels[ch].note = SearchSsgNote(ftone);
                }
                else
                {
                    newParam.channels[ch].note = -1;
                }

                newParam.channels[ch].volumeL = System.Math.Min(System.Math.Max((int)(psgVol[ch][0] / (15.0 / 19.0)), 0), 19);
                newParam.channels[ch].volumeR = System.Math.Min(System.Math.Max((int)(psgVol[ch][1] / (15.0 / 19.0)), 0), 19);
                newParam.channels[ch].volume = System.Math.Max(psgVol[ch][0], psgVol[ch][1]);
                newParam.channels[ch].pan = (psgRegisterPan >> ch) & 0x11;
                newParam.channels[ch].pan = (newParam.channels[ch].pan & 0x1) | (newParam.channels[ch].pan >> 3);
            }

            // Noise channel.
            newParam.channels[3].note = psgRegister[6];
            newParam.channels[3].freq = psgRegister[4];
            newParam.channels[3].volumeL = System.Math.Min(System.Math.Max((int)(psgVol[3][0] / (15.0 / 19.0)), 0), 19);
            newParam.channels[3].volumeR = System.Math.Min(System.Math.Max((int)(psgVol[3][1] / (15.0 / 19.0)), 0), 19);
            newParam.channels[3].volume = System.Math.Max(psgVol[3][0], psgVol[3][1]);
            newParam.channels[3].pan = (psgRegisterPan >> 3) & 0x11;
            newParam.channels[3].pan = (newParam.channels[3].pan & 0x1) | (newParam.channels[3].pan >> 3);
        }

        // frmSN76489.cs:194 ScreenDrawParams (primary/non-NGP path only). tp is always 0
        // here (see DrawBuffSn76489.cs's header comment).
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel osc = oldParam.channels[c];
                MDChipParams.Channel nsc = newParam.channels[c];

                DrawBuffSn76489.Volume(screen, 280, 8 + c * 8, 1, ref osc.volumeL, nsc.volumeL);
                DrawBuffSn76489.Volume(screen, 280, 8 + c * 8, 2, ref osc.volumeR, nsc.volumeR);
                DrawBuffSn76489.KeyBoardDCSG(screen, 32, 8 + c * 8, ref osc.note, nsc.note);
                DrawBuffSn76489.ChSN76489(screen, c, ref osc.mask, nsc.mask);
                DrawBuffSn76489.Pan(screen, 24, 8 + c * 8, ref osc.pan, nsc.pan, ref osc.pantp, 0);
                if (osc.volume != nsc.volume)
                {
                    DrawBuffSn76489.DrawFont4(screen, 272, 8 + c * 8, nsc.volume.ToString("00"));
                    osc.volume = nsc.volume;
                }
                DrawBuffSn76489.Font4Hex12Bit(screen, 256, 8 + c * 8, ref osc.freq, nsc.freq);
            }

            {
                MDChipParams.Channel osc = oldParam.channels[3];
                MDChipParams.Channel nsc = newParam.channels[3];
                DrawBuffSn76489.Volume(screen, 280, 8 + 3 * 8, 1, ref osc.volumeL, nsc.volumeL);
                DrawBuffSn76489.Volume(screen, 280, 8 + 3 * 8, 2, ref osc.volumeR, nsc.volumeR);
                if (osc.volume != nsc.volume)
                {
                    DrawBuffSn76489.DrawFont4(screen, 272, 8 + 3 * 8, nsc.volume.ToString("00"));
                    osc.volume = nsc.volume;
                }
                DrawBuffSn76489.ChSN76489(screen, 3, ref osc.mask, nsc.mask);
                DrawBuffSn76489.ChSN76489Noise(screen, ref osc, nsc);
                DrawBuffSn76489.Pan(screen, 24, 8 + 3 * 8, ref osc.pan, nsc.pan, ref osc.pantp, 0);
            }

            screen.Present();
        }

        // frmSN76489.cs:367 SearchSSGNote.
        private static int SearchSsgNote(float freq)
        {
            float m = float.MaxValue;
            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                float a = System.Math.Abs(freq - Tables.freqTbl[i]);
                if (m > a)
                {
                    m = a;
                    n = i;
                }
            }
            return n;
        }
    }
}
