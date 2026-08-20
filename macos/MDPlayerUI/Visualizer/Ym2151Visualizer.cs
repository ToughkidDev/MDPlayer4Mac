// Port of MDPlayer/MDPlayerx64/form/KB/frmYM2151.cs - the YM2151 (OPM) channel visualizer:
// 8 plain FM channel rows (LED volume bars, piano keyboard, pan, per-operator
// instrument/operator-parameter table, Key Code/Key Fraction hex readouts) plus the
// chip-wide Noise Enable/Frequency and hardware-LFO (frequency/waveform/AMD/PMD/sync)
// readouts. Unlike YM2612, YM2151 has no PCM channel and no Ch3-style extended-slot mode,
// so this visualizer's shape is simpler - every channel takes the same draw path.
//
// Data source: reads chipRegister.fmRegisterYM2151/fmKeyOnYM2151/fmVolYM2151/fmAMDYM2151/
// fmPMDYM2151 directly (same approach as Sn76489Visualizer/Ym2612Visualizer), bypassing the
// original's unported Audio.GetYM2151Register/GetYM2151KeyOn/GetYM2151Volume/
// GetYM2151AMD/GetYM2151PMD wrappers - those are thin pass-throughs to these same
// ChipRegister fields.
//
// Deliberate simplification - YM2151Hosei: the original's note calculation
// (frmYM2151.cs:222-228) adds a `hosei` correction sourced from
// Audio.DriverVirtual.YM2151Hosei[chipID], a per-driver-instance note-frequency correction
// (see baseDriver.cs's YM2151Hosei field / SetYM2151Hosei method) that's computed from the
// chip's actual clock rate vs. its nominal clock and only affects which piano key gets
// highlighted (a small note-index offset), not any audible output. That value isn't stored
// anywhere on ChipRegister itself (confirmed by reading setYM2151Register's body - the
// `hosei` parameter it receives is only forwarded to midiExport.OutMIDIData for MIDI
// export, never written back to a ChipRegister field), so it isn't reachable from this
// port's "read ChipRegister directly" pattern without threading driver-instance state
// through the visualizer constructor. This port hardcodes hosei=0, which matches the
// original's own fallback (frmYM2151.cs:223-227: hosei defaults to 0 whenever
// Audio.DriverVirtual is null) - worst case this is a one-or-two-note keyboard-highlight
// offset, never a wrong register/volume/instrument-table reading.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2151Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly int ChipID;

        private readonly MDChipParams.YM2151 newParam = new();
        private readonly MDChipParams.YM2151 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYM2151.cs:176 md[] - per-algorithm bitmask of which operator(s) are the
        // audible "carrier" output(s) (bit layout per the original's comment: bit6=C2,
        // bit5=M2, bit4=C1, bit3=M1), used by the channel volume-bar estimate below. Same
        // role as DrawBuffYm2612/Ym2612Visualizer's Md table but transcribed exactly from
        // frmYM2151.cs's own copy - OPM's table differs from OPN2's.
        private static readonly byte[] Md = new byte[]
        {
            0x40, 0x40, 0x40, 0x40, 0x50, 0x70, 0x70, 0x78,
        };

        // `clockHz` is accepted (unused) only to keep this constructor's shape uniform with
        // Sn76489Visualizer/Ym2612Visualizer for MainWindow.axaml.cs's per-chip wiring
        // block - unlike those chips, YM2151's note readout comes straight from its Key
        // Code register (see ScreenChangeParams below), not a clock-derived frequency
        // calculation, so the original frmYM2151.cs never needs the chip clock either.
        public Ym2151Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;

            DrawBuffYm2151.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeE");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2151.cs:186 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[] ym2151Register = chipRegister.fmRegisterYM2151[ChipID];
            int[] fmKey = chipRegister.fmKeyOnYM2151[ChipID];
            int[] fmVol = chipRegister.fmVolYM2151[ChipID];

            if (ym2151Register == null || fmKey == null || fmVol == null) return;

            for (int ch = 0; ch < 8; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];

                for (int i = 0; i < 4; i++)
                {
                    int ops = i == 0 ? 0 : (i == 1 ? 16 : (i == 2 ? 8 : 24));
                    nyc.inst[i * 11 + 0] = ym2151Register[0x80 + ops + ch] & 0x1f; // AR
                    nyc.inst[i * 11 + 1] = ym2151Register[0xa0 + ops + ch] & 0x1f; // DR
                    nyc.inst[i * 11 + 2] = ym2151Register[0xc0 + ops + ch] & 0x1f; // SR
                    nyc.inst[i * 11 + 3] = ym2151Register[0xe0 + ops + ch] & 0x0f; // RR
                    nyc.inst[i * 11 + 4] = (ym2151Register[0xe0 + ops + ch] & 0xf0) >> 4; // SL
                    nyc.inst[i * 11 + 5] = ym2151Register[0x60 + ops + ch] & 0x7f; // TL
                    nyc.inst[i * 11 + 6] = (ym2151Register[0x80 + ops + ch] & 0xc0) >> 6; // KS
                    nyc.inst[i * 11 + 7] = ym2151Register[0x40 + ops + ch] & 0x0f; // ML
                    nyc.inst[i * 11 + 8] = (ym2151Register[0x40 + ops + ch] & 0x70) >> 4; // DT
                    nyc.inst[i * 11 + 9] = (ym2151Register[0xc0 + ops + ch] & 0xc0) >> 6; // DT2
                    nyc.inst[i * 11 + 10] = (ym2151Register[0xa0 + ops + ch] & 0x80) >> 7; // AM
                }
                nyc.inst[44] = ym2151Register[0x20 + ch] & 0x07; // AL
                nyc.inst[45] = (ym2151Register[0x20 + ch] & 0x38) >> 3; // FB
                nyc.inst[46] = ym2151Register[0x38 + ch] & 0x3; // AMS
                nyc.inst[47] = (ym2151Register[0x38 + ch] & 0x70) >> 4; // PMS

                nyc.slot = (byte)(fmKey[ch] >> 3);
                int p = (ym2151Register[0x20 + ch] & 0xc0) >> 6;
                nyc.pan = p == 1 ? 2 : (p == 2 ? 1 : p);

                int note = ym2151Register[0x28 + ch] & 0x0f;
                note = note < 3 ? note : (note < 7 ? note - 1 : (note < 11 ? note - 2 : note - 3));
                int oct = (ym2151Register[0x28 + ch] & 0x70) >> 4;
                const int hosei = 0; // see file header - not reachable from ChipRegister alone.
                nyc.note = (fmKey[ch] & 1) != 0 ? (oct * 12 + note + hosei) : -1;

                byte con = (byte)fmKey[ch];
                int v = 127;
                byte m = Md[ym2151Register[0x20 + ch] & 7];
                byte carrierOp = (byte)(con & m);

                v = ((carrierOp & 0x08) != 0) && v > (ym2151Register[0x60 + ch] & 0x7f) ? ym2151Register[0x60 + ch] & 0x7f : v;
                v = ((carrierOp & 0x10) != 0) && v > (ym2151Register[0x68 + ch] & 0x7f) ? ym2151Register[0x68 + ch] & 0x7f : v;
                v = ((carrierOp & 0x20) != 0) && v > (ym2151Register[0x70 + ch] & 0x7f) ? ym2151Register[0x70 + ch] & 0x7f : v;
                v = ((carrierOp & 0x40) != 0) && v > (ym2151Register[0x78 + ch] & 0x7f) ? ym2151Register[0x78 + ch] & 0x7f : v;

                nyc.volumeL = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((ym2151Register[0x20 + ch] & 0x80) != 0 ? 1 : 0) * fmVol[ch] / 80.0), 0), 19);
                nyc.volumeR = System.Math.Min(System.Math.Max((int)((127 - v) / 127.0 * ((ym2151Register[0x20 + ch] & 0x40) != 0 ? 1 : 0) * fmVol[ch] / 80.0), 0), 19);

                nyc.kc = ym2151Register[0x28 + ch] & 0x7f;
                nyc.kf = (ym2151Register[0x30 + ch] & 0xfc) >> 2;
            }

            newParam.ne = (ym2151Register[0x0f] & 0x80) >> 7;
            newParam.nfrq = (ym2151Register[0x0f] & 0x1f) >> 0;
            newParam.lfrq = ym2151Register[0x18] & 0xff;
            newParam.pmd = chipRegister.fmPMDYM2151[ChipID];
            newParam.amd = chipRegister.fmAMDYM2151[ChipID];
            newParam.timerA = ym2151Register[0x10] | ((ym2151Register[0x11] & 0x3) << 8);
            newParam.timerB = ym2151Register[0x12];
            newParam.waveform = (ym2151Register[0x1b] & 0x3) >> 0;
            newParam.lfosync = (ym2151Register[0x01] & 0x02) >> 1;
        }

        // frmYM2151.cs:264 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 8; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffYm2151.InstOPM(screen, 9, 11, c, oyc.inst, nyc.inst);

                DrawBuffYm2151.Pan(screen, 25, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                DrawBuffYm2151.KeyBoardOPM(screen, c, ref oyc.note, nyc.note);

                DrawBuffYm2151.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                DrawBuffYm2151.Volume(screen, 68 * 4 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2151.Volume(screen, 68 * 4 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);

                DrawBuffYm2151.ChYM2151(screen, c, ref oyc.mask, nyc.mask);

                DrawBuffYm2151.KcYM2151(screen, c, ref oyc.kc, nyc.kc);
                DrawBuffYm2151.KfYM2151(screen, c, ref oyc.kf, nyc.kf);
            }

            DrawBuffYm2151.NeYM2151(screen, ref oldParam.ne, newParam.ne);
            DrawBuffYm2151.NfrqYM2151(screen, ref oldParam.nfrq, newParam.nfrq);
            DrawBuffYm2151.LfrqYM2151(screen, ref oldParam.lfrq, newParam.lfrq);
            DrawBuffYm2151.AmdYM2151(screen, ref oldParam.amd, newParam.amd);
            DrawBuffYm2151.PmdYM2151(screen, ref oldParam.pmd, newParam.pmd);

            DrawBuffYm2151.Font4Hex12Bit(screen, 82 * 4 + 1, 22 * 8, ref oldParam.timerA, newParam.timerA);
            DrawBuffYm2151.Font4HexByte(screen, 82 * 4 + 1, 23 * 8, ref oldParam.timerB, newParam.timerB);
            DrawBuffYm2151.WaveFormYM2151(screen, ref oldParam.waveform, newParam.waveform);
            DrawBuffYm2151.LfoSyncYM2151(screen, ref oldParam.lfosync, newParam.lfosync);

            screen.Present();
        }
    }
}
