// Port of MDPlayer/MDPlayerx64/form/KB/NES/frmNESDMC.cs - the NES/Famicom APU channel
// visualizer. 5 channels: 2 pulse/square (indices 0-1), 1 triangle, 1 noise, 1 DMC (delta-
// modulation sample playback) - each with its own hand-placed fixed screen position rather
// than a uniform per-row grid, the smallest/most bespoke layout of any chip ported so far.
//
// Data source: reads chipRegister.GetAPURegister(chipID) (2 pulse channels' 4 registers
// each) and chipRegister.GetDMCRegister(chipID) (triangle/noise/DMC registers packed into
// one array, plus a frame-counter byte at index 13) - raw register bytes, added to
// ChipRegister.cs alongside this file since no earlier chip needed them (see that file's
// GetAPURegister/GetDMCRegister comment).
//
// Deliberate simplification: `tp` hardcoded 0, same as every other DrawBuffXxx.cs in this
// port. screenInit's placeholder pre-draw loop (blank piano keys + unmask every badge) is
// skipped, same as every other chip in this port - the first real ScreenDrawParams call
// naturally draws everything via the normal dirty-diff path.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class NesdmcVisualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly int ChipID;

        private readonly MDChipParams.NESDMC newParam = new();
        private readonly MDChipParams.NESDMC oldParam = new();

        public PixelScreen Screen => screen;

        // frmNESDMC.cs:90-92.
        private const double Log2_440 = 8.7813597135246596040696824762152;
        private const double Log_2 = 0.69314718055994530941723212145818;
        private const int Note440Hz = 12 * 4 + 9;

        public NesdmcVisualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            _ = clockHz; // frmNESDMC.cs's screenChangeParams never reads a clock value.

            DrawBuffNesdmc.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeNESDMC");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmNESDMC.cs:88 screenChangeParams.
        public void ScreenChangeParams()
        {
            byte[] reg = chipRegister.GetAPURegister(ChipID);
            if (reg != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    MDChipParams.Channel channel = newParam.sqrChannels[i];
                    int freq = (reg[3 + i * 4] & 0x07) * 0x100 + reg[2 + i * 4];
                    int vol = reg[i * 4] & 0xf;
                    int note = 104 - (int)(12 * (Math.Log(freq) / Log_2 - Log2_440) + Note440Hz + 0.5);
                    note = vol == 0 ? -1 : note;
                    channel.note = note;
                    channel.volume = Math.Min((int)(vol * 1.33), 19);
                    channel.nfrq = (reg[1 + i * 4] & 0x70) >> 4; // Period
                    channel.pan = reg[1 + i * 4] & 0x07; // Shift
                    channel.pantp = (reg[3 + i * 4] & 0xf8) >> 3; // Length counter load
                    channel.kf = (reg[i * 4] & 0xc0) >> 6; // Duty
                    channel.dda = ((reg[i * 4] & 0x20) >> 5) != 0; // LengthCounter
                    channel.noise = ((reg[i * 4] & 0x10) >> 4) != 0; // constantVolume
                    channel.volumeL = (reg[1 + i * 4] & 0x80) >> 7; // Sweep unit enabled
                    channel.volumeR = (reg[1 + i * 4] & 0x08) >> 3; // negate
                }
            }

            byte[] reg2 = chipRegister.GetDMCRegister(ChipID);
            if (reg2 == null) return;

            int tri = reg2[0x10];
            int noi = reg2[0x11];
            int dpc = reg2[0x12];

            int dmcFreq = (reg2[3] & 0x07) * 0x100 + reg2[2];
            int dmcNote = 92 - (int)(12 * (Math.Log(dmcFreq) / Log_2 - Log2_440) + Note440Hz + 0.5);
            newParam.triChannel.note = (reg2[0] & 0x7f) == 0 ? -1 : dmcNote;
            if ((reg2[0] & 0x80) == 0)
            {
                if ((reg2[13] & 0x04) == 0)
                    newParam.triChannel.note = -1;
            }

            newParam.dmcChannel.volumeR = reg2[9] & 0x7f; // Load counter
            tri = tri == 0 ? 0 : 10 + (128 - newParam.dmcChannel.volumeR) / 128 * 9;
            newParam.triChannel.volume = newParam.triChannel.note < 0 ? 0 : tri;
            newParam.triChannel.dda = (reg2[0] & 0x80) != 0; // LengthCounterHalt
            newParam.triChannel.nfrq = reg2[0] & 0x7f; // linear counter load (R)
            newParam.triChannel.pantp = (reg2[3] & 0xf8) >> 3; // Length counter load

            newParam.noiseChannel.volume = Math.Min((int)((reg2[4] & 0xf) * 1.33), 19);
            newParam.noiseChannel.dda = (reg2[4] & 0x20) != 0; // Envelope loop / length counter halt
            newParam.noiseChannel.noise = (reg2[4] & 0x10) != 0; // constant volume
            newParam.noiseChannel.volumeL = (reg2[6] & 0x80) >> 7; // Loop noise
            newParam.noiseChannel.volumeR = reg2[6] & 0x0f; // noise period
            newParam.noiseChannel.nfrq = (reg2[7] & 0xf8) >> 3; // Length counter load
            noi = noi == 0 ? 0 : 1;
            newParam.noiseChannel.volume =
                (reg2[13] & 0x8) != 0
                    ? ((reg2[4] & 0x10) != 0 ? newParam.noiseChannel.volume * noi : (10 + (128 - newParam.dmcChannel.volumeR) / 128 * 9) * noi)
                    : 0;

            dpc = dpc == 0 ? 0 : 10 + (128 - newParam.dmcChannel.volumeR) / 128 * 9;
            newParam.dmcChannel.dda = (reg2[8] & 0x80) != 0; // IRQ enable
            newParam.dmcChannel.noise = (reg2[8] & 0x40) != 0; // loop
            newParam.dmcChannel.volumeL = reg2[8] & 0x0f; // frequency
            newParam.dmcChannel.nfrq = reg2[10]; // Sample address
            newParam.dmcChannel.pantp = reg2[11]; // Sample length
            newParam.dmcChannel.volume = (reg2[13] & 0x10) == 0 ? 0 : dpc;
        }

        // frmNESDMC.cs:174 screenDrawParams.
        public void ScreenDrawParams()
        {
            bool ob;
            for (int i = 0; i < 2; i++)
            {
                MDChipParams.Channel oc = oldParam.sqrChannels[i];
                MDChipParams.Channel nc = newParam.sqrChannels[i];

                DrawBuffNesdmc.KeyBoard(screen, i * 2, ref oc.note, nc.note);
                DrawBuffNesdmc.Volume(screen, 256, 8 + i * 2 * 8, 0, ref oc.volume, nc.volume);
                DrawBuffNesdmc.Font4Int2(screen, 16 * 4, (2 + i * 2) * 8, ref oc.nfrq, nc.nfrq);
                DrawBuffNesdmc.Font4Int2(screen, 19 * 4, (2 + i * 2) * 8, ref oc.pan, nc.pan);
                DrawBuffNesdmc.Font4Int2(screen, 22 * 4, (2 + i * 2) * 8, ref oc.pantp, nc.pantp);
                DrawBuffNesdmc.DrawDuty(screen, 24, (1 + i * 2) * 8, ref oc.kf, nc.kf);
                DrawBuffNesdmc.DrawNesSw(screen, 32, (2 + i * 2) * 8, ref oc.dda, nc.dda);
                DrawBuffNesdmc.DrawNesSw(screen, 40, (2 + i * 2) * 8, ref oc.noise, nc.noise);
                ob = oc.volumeL != 0;
                DrawBuffNesdmc.DrawNesSw(screen, 48, (2 + i * 2) * 8, ref ob, nc.volumeL != 0);
                oc.volumeL = ob ? 1 : 0;
                ob = oc.volumeR != 0;
                DrawBuffNesdmc.DrawNesSw(screen, 56, (2 + i * 2) * 8, ref ob, nc.volumeR != 0);
                oc.volumeR = ob ? 1 : 0;
                DrawBuffNesdmc.ChNesdmc(screen, i, ref oc.mask, nc.mask);
            }

            DrawBuffNesdmc.KeyBoard(screen, 4, ref oldParam.triChannel.note, newParam.triChannel.note);
            DrawBuffNesdmc.Volume(screen, 256, 8 + 4 * 8, 0, ref oldParam.triChannel.volume, newParam.triChannel.volume);
            DrawBuffNesdmc.DrawNesSw(screen, 36, 6 * 8, ref oldParam.triChannel.dda, newParam.triChannel.dda);
            DrawBuffNesdmc.Font4Int3(screen, 13 * 4, 6 * 8, ref oldParam.triChannel.nfrq, newParam.triChannel.nfrq);
            DrawBuffNesdmc.Font4Int2(screen, 19 * 4, 6 * 8, ref oldParam.triChannel.pantp, newParam.triChannel.pantp);
            DrawBuffNesdmc.ChNesdmc(screen, 2, ref oldParam.triChannel.mask, newParam.triChannel.mask);

            DrawBuffNesdmc.Volume(screen, 256, 8 + 3 * 8, 0, ref oldParam.noiseChannel.volume, newParam.noiseChannel.volume);
            DrawBuffNesdmc.DrawNesSw(screen, 228, 32, ref oldParam.noiseChannel.dda, newParam.noiseChannel.dda);
            DrawBuffNesdmc.DrawNesSw(screen, 144, 32, ref oldParam.noiseChannel.noise, newParam.noiseChannel.noise);
            bool obN = oldParam.noiseChannel.volumeL != 0;
            DrawBuffNesdmc.DrawNesSw(screen, 160, 32, ref obN, newParam.noiseChannel.volumeL != 0);
            oldParam.noiseChannel.volumeL = obN ? 1 : 0;
            DrawBuffNesdmc.Font4Int2(screen, 176, 32, ref oldParam.noiseChannel.volumeR, newParam.noiseChannel.volumeR);
            DrawBuffNesdmc.Font4Int2(screen, 196, 32, ref oldParam.noiseChannel.nfrq, newParam.noiseChannel.nfrq);
            DrawBuffNesdmc.ChNesdmc(screen, 3, ref oldParam.noiseChannel.mask, newParam.noiseChannel.mask);

            DrawBuffNesdmc.Volume(screen, 256, 8 + 5 * 8, 0, ref oldParam.dmcChannel.volume, newParam.dmcChannel.volume);
            DrawBuffNesdmc.DrawNesSw(screen, 144, 48, ref oldParam.dmcChannel.dda, newParam.dmcChannel.dda);
            // frmNESDMC.cs:216 genuinely reuses .dda as the "old" tracker for the .noise
            // (loop) switch here too, not a separate old-value field - transcribed as-is.
            DrawBuffNesdmc.DrawNesSw(screen, 152, 48, ref oldParam.dmcChannel.dda, newParam.dmcChannel.noise);
            DrawBuffNesdmc.Font4Int2(screen, 176, 48, ref oldParam.dmcChannel.volumeL, newParam.dmcChannel.volumeL);
            DrawBuffNesdmc.Font4Int3(screen, 192, 48, ref oldParam.dmcChannel.volumeR, newParam.dmcChannel.volumeR);
            DrawBuffNesdmc.Font4HexByte(screen, 220, 48, ref oldParam.dmcChannel.nfrq, newParam.dmcChannel.nfrq);
            DrawBuffNesdmc.Font4HexByte(screen, 244, 48, ref oldParam.dmcChannel.pantp, newParam.dmcChannel.pantp);
            DrawBuffNesdmc.ChNesdmc(screen, 4, ref oldParam.dmcChannel.mask, newParam.dmcChannel.mask);

            screen.Present();
        }
    }
}
