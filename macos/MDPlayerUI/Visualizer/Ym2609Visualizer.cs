// Port of MDPlayer/MDPlayerx64/form/KB/OPN/frmYM2609.cs - the YM2609 channel visualizer.
// YM2609 is a virtual "dual OPNA": two YM2608-like FM+SSG cores sharing one register-port
// address space (4 ports total: FM core A on port 0, its rhythm+ADPCM-A extension on port
// 1, FM core B on port 2, its rhythm+ADPCM-A extension on port 3), plus a stereo 3-band EQ
// and 3 extra single-channel ADPCM (ADPCM012) units that YM2608 doesn't have. 45 channels
// total in this port's MDChipParams.YM2609.channels layout: FM 0-17 (2x9, each with its own
// Ch3-extended-mode slot group at index 2/8), SSG 18-29 (4 SSG cores x 3 channels, via the
// psgPort/psgAdr tables below - same "4 independent tone generators sharing register space"
// shape as the FM side), RHYTHM 30-35, ADPCM012 36-38, ADPCM-A 39-44.
//
// Index-layout note: the original frmYM2609.cs numbers its channels FM 0-17, SSG 18-29,
// RHYTHM 30-35, ADPCM-A 36-41, ADPCM012 42-44 - ADPCM-A *before* ADPCM012. This port's
// already-established MDChipParams.YM2609.channels array (see MDChipParams.cs) was built
// with ADPCM012 at 36-38 and ADPCM-A at 39-44 instead - the two groups swapped. Since
// nothing else in this port reads MDChipParams.YM2609 yet, this file follows the
// already-shipped array's layout rather than renumbering it, and adjusts the loop math
// accordingly (see ScreenChangeParams's RHYTHM/ADPCM-A/ADPCM012 sections). The channel
// *content* (which register bits feed which channel) is unchanged from the original -
// only the array index each group lands at differs.
//
// Data source: reads chipRegister.fmRegisterYM2609[chipID] (int[4][0x100]) and
// chipRegister.fmKeyOnYM2609[chipID] (int[12]) directly, plus chipRegister.GetYM2609Volume,
// GetYM2609Ch3SlotVolume, GetYM2609RhythmVolume (int[12][2] - rhythm's 6 voices then
// ADPCM-A's 6 voices, in that order, matching the original), GetYM2609AdpcmAPan/
// GetYM2609AdpcmAVol (int[6] each), GetYM2609AdpcmVolume/GetYM2609AdpcmPan (int[3][2] each,
// for the 3 ADPCM012 units), and readYM2609GetUserWave(chipID, p, n, model) for the PSG
// custom-wavetable bytes. readYM2609GetUserWave takes an EnmModel that only matters to
// suppress piano-roll-specific reads (returns null under EnmModel.PianoRollModel) - this
// port always passes EnmModel.VirtualModel, matching ordinary (non-piano-roll) emulated
// playback.
//
// Deliberate simplifications, same rationale as this port's other OPN-family files (see
// YM2203Visualizer.cs/Ym2608Visualizer.cs headers): `tp` (hardware-chip-type) hardcoded to
// 0, and `parent.setting.other.ExAll` (never wired through to the Visualizer layer in this
// port) hardcoded to its original default `false` in both places frmYM2609.cs reads it.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ym2609Visualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly float clockHz;
        private const int ChipID = 0;

        private readonly MDChipParams.YM2609 newParam = new();
        private readonly MDChipParams.YM2609 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYM2609.cs:97-112.
        private static readonly byte[] Md = { 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x08 << 4, 0x0c << 4, 0x0e << 4, 0x0e << 4, 0x0f << 4 };
        private static readonly float[] FmDivTbl = { 6, 3, 2 };
        private static readonly float[] SsgDivTbl = { 4, 2, 1 };
        private readonly bool[] isFmEx = { false, false };
        private static readonly int[] ExReg = { 2, 0, -6 };
        private static readonly int[] PsgPort = { 0, 1, 2, 2 };
        private static readonly int[] PsgAdr = { 0x00, 0x20, 0x00, 0x10 };

        public Ym2609Visualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            this.clockHz = clockHz;

            DrawBuffYm2609.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYM2609");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYM2609.cs:114 screenChangeParams.
        public void ScreenChangeParams()
        {
            int[][] ym2609Register = chipRegister.fmRegisterYM2609[ChipID];
            if (ym2609Register == null) return;

            int[] fmKeyYM2609 = chipRegister.fmKeyOnYM2609[ChipID];
            int[] ym2609Vol = chipRegister.GetYM2609Volume(ChipID);
            int[] ym2609Ch3SlotVol = chipRegister.GetYM2609Ch3SlotVolume(ChipID);
            int[][] ym2609Rhythm = chipRegister.GetYM2609RhythmVolume(ChipID);
            int[] ym2609AdpcmAPan = chipRegister.GetYM2609AdpcmAPan(ChipID);
            int[] ym2609AdpcmAVol = chipRegister.GetYM2609AdpcmAVol(ChipID);
            int[][] ym2609AdpcmVol = chipRegister.GetYM2609AdpcmVolume(ChipID);
            int[][] ym2609AdpcmPan = chipRegister.GetYM2609AdpcmPan(ChipID);

            isFmEx[0] = (ym2609Register[0][0x27] & 0x40) > 0;
            isFmEx[1] = (ym2609Register[2][0x27] & 0x40) > 0;
            newParam.channels[2].ex = isFmEx[0];
            newParam.channels[8].ex = isFmEx[1];

            int defaultMasterClock = 7987200;
            float ssgMul = 1.0f;
            int masterClock = defaultMasterClock;
            if (clockHz != 0)
            {
                ssgMul = clockHz / (float)defaultMasterClock;
                masterClock = (int)clockHz;
            }

            int divInd = ym2609Register[0][0x2d];
            if (divInd < 0 || divInd > 2) divInd = 0;
            float fmDiv = FmDivTbl[divInd];
            float ssgDiv = SsgDivTbl[divInd];
            ssgMul = ssgMul / ssgDiv * 4;

            newParam.timerA = ym2609Register[0][0x24] | ((ym2609Register[0][0x25] & 0x3) << 8);
            newParam.timerB = ym2609Register[0][0x26];

            newParam.lfoSw[0] = (ym2609Register[0][0x22] & 0x8) != 0;
            newParam.lfoFrq[0] = ym2609Register[0][0x22] & 0x7;
            newParam.lfoSw[1] = (ym2609Register[2][0x22] & 0x8) != 0;
            newParam.lfoFrq[1] = ym2609Register[2][0x22] & 0x7;

            newParam.rhythmTotalLevel[0] = ym2609Register[0][0x11];
            newParam.rhythmTotalLevel[1] = ym2609Register[1][0x12];
            newParam.adpcmLevel[0] = ym2609Register[1][0x0b];
            newParam.adpcmLevel[1] = ym2609Register[3][0x0b];
            newParam.adpcmLevel[2] = ym2609Register[3][0x1c];

            newParam.eqLowSw = ym2609Register[0][0xc0] != 0;
            newParam.eqLow[0] = ym2609Register[0][0xc1];
            newParam.eqLow[1] = ym2609Register[0][0xc2];
            newParam.eqLow[2] = ym2609Register[0][0xc3];

            newParam.eqMidSw = ym2609Register[0][0xc4] != 0;
            newParam.eqMid[0] = ym2609Register[0][0xc5];
            newParam.eqMid[1] = ym2609Register[0][0xc6];
            newParam.eqMid[2] = ym2609Register[0][0xc7];

            newParam.eqHiSw = ym2609Register[0][0xc8] != 0;
            newParam.eqHi[0] = ym2609Register[0][0xc9];
            newParam.eqHi[1] = ym2609Register[0][0xca];
            newParam.eqHi[2] = ym2609Register[0][0xcb];

            // FM (2 sub-cores x 9 channels, p = sub-core register-port pair, c = in-core slot).
            for (int ch = 0; ch < 12; ch++)
            {
                int p = ch / 3;
                int c = ch % 3;
                MDChipParams.Channel channel = newParam.channels[ch];

                for (int i = 0; i < 4; i++)
                {
                    int ops = i == 0 ? 0 : (i == 1 ? 8 : (i == 2 ? 4 : 12));
                    channel.inst[i * 16 + 0] = ym2609Register[p][0x50 + ops + c] & 0x1f; // AR
                    channel.inst[i * 16 + 1] = ym2609Register[p][0x60 + ops + c] & 0x1f; // DR
                    channel.inst[i * 16 + 2] = ym2609Register[p][0x70 + ops + c] & 0x1f; // SR
                    channel.inst[i * 16 + 3] = ym2609Register[p][0x80 + ops + c] & 0x0f; // RR
                    channel.inst[i * 16 + 4] = (ym2609Register[p][0x80 + ops + c] & 0xf0) >> 4; // SL
                    channel.inst[i * 16 + 5] = ym2609Register[p][0x40 + ops + c] & 0x7f; // TL
                    channel.inst[i * 16 + 6] = (ym2609Register[p][0x50 + ops + c] & 0xc0) >> 6; // KS
                    channel.inst[i * 16 + 7] = ym2609Register[p][0x30 + ops + c] & 0x0f; // ML
                    channel.inst[i * 16 + 8] = (ym2609Register[p][0x30 + ops + c] & 0x70) >> 4; // DT
                    channel.inst[i * 16 + 9] = (ym2609Register[p][0x60 + ops + c] & 0x60) >> 5; // D2
                    channel.inst[i * 16 + 10] = (ym2609Register[p][0x60 + ops + c] & 0x80) >> 7; // AM
                    channel.inst[i * 16 + 11] = ym2609Register[p][0x90 + ops + c] & 0x0f; // SG
                    channel.inst[i * 16 + 12] = i == 0
                        ? (ym2609Register[p][0xb0 + c] & 0x38) >> 3
                        : (ym2609Register[p][0x70 + ops + c] & 0xe0) >> 5; // FB
                    channel.inst[i * 16 + 13] =
                        ((ym2609Register[p][0x30 + ops + c] & 0x80) >> 7)
                        + ((ym2609Register[p][0x40 + ops + c] & 0x80) >> 6); // WT
                    channel.inst[i * 16 + 14] = (ym2609Register[p][0x90 + ops + c] & 0xf0) >> 4; // ALL
                    channel.inst[i * 16 + 15] = (ym2609Register[p][0x50 + ops + c] & 0x20) >> 5; // PR
                }
                channel.inst[64] = ym2609Register[p][0xb0 + c] & 0x07; // AL
                channel.inst[65] = (ym2609Register[p][0xb4 + c] & 0x38) >> 4; // AMS
                channel.inst[66] = ym2609Register[p][0xb4 + c] & 0x07; // FMS

                int pan = (ym2609Register[p][0xb4 + c] & 0xc0) >> 6;
                channel.pan =
                    (((pan & 0x02) != 0) ? (3 - ((ym2609Register[p][0xa4 + c] & 0xc0) >> 6) + 1) : 0) * 5
                    + (((pan & 0x01) != 0) ? (3 - ((ym2609Register[p][0xb0 + c] & 0xc0) >> 6) + 1) : 0);
                channel.slot = (byte)(fmKeyYM2609[ch] >> 4);

                int freq;
                int octav;
                int n = -1;

                if ((ch != 2 || !isFmEx[0]) && (ch != 8 || !isFmEx[1]))
                {
                    octav = (ym2609Register[p][0xa4 + c] & 0x38) >> 3;
                    freq = ym2609Register[p][0xa0 + c] + (ym2609Register[p][0xa4 + c] & 0x07) * 0x100;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKeyYM2609[ch] & 1) != 0)
                        n = Math.Min(Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    byte con = (byte)fmKeyYM2609[ch];
                    int v = 127;
                    int m = Md[ym2609Register[p][0xb0 + c] & 7];
                    v = ((con & 0x10) != 0 && (m & 0x10) != 0 && v > (ym2609Register[p][0x40 + c] & 0x7f)) ? (ym2609Register[p][0x40 + c] & 0x7f) : v; // OP1
                    v = ((con & 0x20) != 0 && (m & 0x20) != 0 && v > (ym2609Register[p][0x44 + c] & 0x7f)) ? (ym2609Register[p][0x44 + c] & 0x7f) : v; // OP3
                    v = ((con & 0x40) != 0 && (m & 0x40) != 0 && v > (ym2609Register[p][0x48 + c] & 0x7f)) ? (ym2609Register[p][0x48 + c] & 0x7f) : v; // OP2
                    v = ((con & 0x80) != 0 && (m & 0x80) != 0 && v > (ym2609Register[p][0x4c + c] & 0x7f)) ? (ym2609Register[p][0x4c + c] & 0x7f) : v; // OP4

                    int panL = (ym2609Register[p][0xb4 + c] & 0x80) == 0 ? 0 : 4 - ((ym2609Register[p][0xa4 + c] & 0xc0) >> 6);
                    int panR = (ym2609Register[p][0xb4 + c] & 0x40) == 0 ? 0 : 4 - ((ym2609Register[p][0xb0 + c] & 0xc0) >> 6);

                    channel.volumeL = Math.Min(Math.Max((int)((127 - v) / 127.0 * panL / 4.0 * ym2609Vol[ch] / 80.0), 0), 19);
                    channel.volumeR = Math.Min(Math.Max((int)((127 - v) / 127.0 * panR / 4.0 * ym2609Vol[ch] / 80.0), 0), 19);
                }
                else
                {
                    // ExAll hardcoded false (see file header) - m is always the
                    // chip-register-computed mask.
                    int m = Md[ym2609Register[p][0xb0 + 2] & 7];
                    freq = ym2609Register[p][0xa9] + (ym2609Register[p][0xad] & 0x07) * 0x100;
                    octav = (ym2609Register[p][0xad] & 0x38) >> 3;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                    ff /= 1038f;

                    if ((fmKeyYM2609[ch] & 0x10) > 0 && (m & 0x10) != 0)
                        n = Math.Min(Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);

                    int v = (m & 0x10) != 0 ? (ym2609Register[p][0x40 + c] & 0x7f) : 127;

                    int panL3 = (ym2609Register[p][0xb4 + 2] & 0x80) == 0 ? 0 : 4 - ((ym2609Register[p][0xa4 + 2] & 0xc0) >> 6);
                    int panR3 = (ym2609Register[p][0xb4 + 2] & 0x40) == 0 ? 0 : 4 - ((ym2609Register[p][0xb0 + 2] & 0xc0) >> 6);

                    channel.volumeL = Math.Min(Math.Max((int)((127 - v) / 127.0 * panL3 / 4.0 * ym2609Ch3SlotVol[0] / 80.0), 0), 19);
                    channel.volumeR = Math.Min(Math.Max((int)((127 - v) / 127.0 * panR3 / 4.0 * ym2609Ch3SlotVol[0] / 80.0), 0), 19);
                }
                channel.note = n;
            }

            // FM EX (both sub-cores' extended-mode op slots, channels 12-17).
            for (int ch = 12; ch < 18; ch++)
            {
                int c = ExReg[ch % 3];
                int p = (ch - 12) / 3 * 2;
                MDChipParams.Channel channel = newParam.channels[ch];
                channel.pan = 0;

                int f = ch < 15 ? 0 : 1;
                if (isFmEx[f])
                {
                    // ExAll hardcoded false (see file header).
                    int m = Md[ym2609Register[p][0xb0 + 2] & 7];
                    int op = ch - (11 + f * 3);
                    op = op == 1 ? 2 : (op == 2 ? 1 : op);

                    int freq = ym2609Register[p][0xa8 + c] + (ym2609Register[p][0xac + c] & 0x07) * 0x100;
                    int octav = (ym2609Register[p][0xac + c] & 0x38) >> 3;
                    channel.freq = (freq & 0x7ff) | ((octav & 7) << 11);
                    int n = -1;
                    if ((fmKeyYM2609[2] & (0x10 << (ch - (11 + f * 3)))) != 0 && (m & (0x10 << op)) != 0)
                    {
                        float ff = freq / ((2 << 20) / (masterClock / (24 * fmDiv))) * (2 << (octav + 2));
                        ff /= 1038f;
                        n = Math.Min(Math.Max(Common.searchYM2608Adpcm(ff) - 1, 0), 95);
                    }
                    channel.note = n;

                    int v = (m & (0x10 << op)) != 0 ? (ym2609Register[p][0x42 + op * 4] & 0x7f) : 127;
                    channel.volumeL = Math.Min(Math.Max((int)((127 - v) / 127.0 * ym2609Ch3SlotVol[ch - (11 - f)] / 80.0), 0), 19);
                }
                else
                {
                    channel.note = -1;
                    channel.volumeL = 0;
                }
            }

            // PSG/SSG (4 independent SSG cores x 3 channels, channels 18-29).
            for (int ch = 18; ch < 30; ch++)
            {
                int c = (ch - 18) % 3;
                int p = (ch - 18) / 3;
                MDChipParams.Channel channel = newParam.channels[ch];

                bool t = (ym2609Register[PsgPort[p]][PsgAdr[p] + 0x07] & (0x1 << c)) == 0;
                bool nz = (ym2609Register[PsgPort[p]][PsgAdr[p] + 0x07] & (0x8 << c)) == 0;
                channel.tn = (t ? 1 : 0) + (nz ? 2 : 0);
                channel.volume = (int)((t || nz ? 1 : 0) * (ym2609Register[PsgPort[p]][PsgAdr[p] + 0x08 + c] & 0xf) * (20.0 / 16.0));
                channel.pan = (ym2609Register[PsgPort[p]][PsgAdr[p] + 0x08 + c] & 0xc0) >> 6;
                if (!t && !nz && channel.volume > 0)
                {
                    channel.volume--;
                }

                channel.note = -1;
                if (channel.volume != 0)
                {
                    int ft = ym2609Register[PsgPort[p]][PsgAdr[p] + 0x00 + c * 2];
                    int ct = ym2609Register[PsgPort[p]][PsgAdr[p] + 0x01 + c * 2] & 0xf;
                    int tp = (ct << 8) | ft;
                    channel.freq = tp;
                    if (tp == 0 && (ym2609Register[PsgPort[p]][PsgAdr[p] + 0x08 + c] & 0x10) == 0)
                    {
                        channel.note = -1;
                    }
                    else
                    {
                        float ftone = masterClock / (64.0f * tp) * ssgMul;
                        channel.note = Common.searchSSGNote(ftone);
                    }
                }

                channel.bank = (ym2609Register[PsgPort[p]][PsgAdr[p] + 0x01 + c * 2] & 0xf0) >> 4;
                channel.PSGWave = channel.bank > 9
                    ? chipRegister.readYM2609GetUserWave(ChipID, p, channel.bank - 10, EnmModel.VirtualModel)
                    : null;
            }

            for (int ch = 0; ch < 4; ch++)
            {
                newParam.nfrq[ch] = ym2609Register[PsgPort[ch]][PsgAdr[ch] + 0x06] & 0x1f;
                newParam.efrq[ch] = ym2609Register[PsgPort[ch]][PsgAdr[ch] + 0x0c] * 0x100 + ym2609Register[PsgPort[ch]][PsgAdr[ch] + 0x0b];
                newParam.etype[ch] = ym2609Register[PsgPort[ch]][PsgAdr[ch] + 0x0d] & 0xf;
            }

            // RHYTHM (channels 30-35 in this port's layout).
            for (int i = 0; i < 6; i++)
            {
                int ch = 30 + i;
                newParam.channels[ch].pan = (ym2609Register[0][0x18 + i] & 0xc0) >> 6;
                newParam.channels[ch].volumeRL = ym2609Register[0][0x18 + i] & 0x1f;
                newParam.channels[ch].volumeL = Math.Min(Math.Max(ym2609Rhythm[i][0] / 80, 0), 19);
                newParam.channels[ch].volumeR = Math.Min(Math.Max(ym2609Rhythm[i][1] / 80, 0), 19);
            }

            // ADPCM-A (channels 39-44 in this port's layout - see file header re: the
            // ADPCM-A/ADPCM012 order swap vs. the original's 36-41/42-44. ym2609Rhythm's
            // entries 6-11 hold the ADPCM-A voices' pre-mixed L/R levels, continuing
            // straight on from the 6 rhythm entries above, same as the original.)
            for (int i = 0; i < 6; i++)
            {
                int ch = 39 + i;
                newParam.channels[ch].pan = ym2609AdpcmAPan[i];
                newParam.channels[ch].volumeRL = ym2609AdpcmAVol[i] & 0x1f;
                newParam.channels[ch].volumeL = Math.Min(Math.Max(ym2609Rhythm[6 + i][0] / 80, 0), 19);
                newParam.channels[ch].volumeR = Math.Min(Math.Max(ym2609Rhythm[6 + i][1] / 80, 0), 19);
            }

            // ADPCM012 (channels 36-38 in this port's layout).
            for (int i = 0; i < 3; i++)
            {
                int ch = 36 + i;
                int p = i == 0 ? 1 : 3;
                int sft = i == 2 ? 0x11 : 0;
                MDChipParams.Channel channel = newParam.channels[ch];

                channel.panL = ym2609AdpcmPan[i][0];
                channel.panR = ym2609AdpcmPan[i][1];
                channel.volumeL = Math.Min(Math.Max(ym2609AdpcmVol[i][0] / 80, 0), 19);
                channel.volumeR = Math.Min(Math.Max(ym2609AdpcmVol[i][1] / 80, 0), 19);
                int delta = (ym2609Register[p][sft + 0x0a] << 8) | ym2609Register[p][sft + 0x09];
                channel.freq = delta;
                float frq = delta / 9447.0f;
                channel.note = (ym2609Register[p][sft + 0x00] & 0x80) != 0 ? Common.searchYM2608Adpcm(frq) - 1 : -1;
                if ((ym2609Register[p][sft + 0x01] & 0xc0) == 0)
                {
                    channel.note = -1;
                }
            }
        }

        // frmYM2609.cs:449 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 18; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                if (c == 2 || c == 8)
                {
                    DrawBuffYm2609.Volume(screen, 288 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2609.Volume(screen, 288 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2609.PanType4(screen, 6 * 4 + 1, 8 + c * 8, ref oyc.pan, nyc.pan, 0);
                    DrawBuffYm2609.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2609.InstOpna2(screen, (c % 4) * 4 * 35 + 368 + 1, (c / 4) * 8 * 6 + 16, oyc.inst, nyc.inst);
                    DrawBuffYm2609.Ch3(screen, c, ref oyc.mask, nyc.mask, ref oyc.ex, nyc.ex);
                    DrawBuffYm2609.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2609.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else if (c < 12)
                {
                    DrawBuffYm2609.Volume(screen, 288 + 1, 8 + c * 8, 1, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2609.Volume(screen, 288 + 1, 8 + c * 8, 2, ref oyc.volumeR, nyc.volumeR);
                    DrawBuffYm2609.PanType4(screen, 6 * 4 + 1, 8 + c * 8, ref oyc.pan, nyc.pan, 0);
                    DrawBuffYm2609.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2609.InstOpna2(screen, (c % 4) * 4 * 35 + 368 + 1, (c / 4) * 8 * 6 + 16, oyc.inst, nyc.inst);
                    DrawBuffYm2609.ChYm2608(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2609.Slot(screen, 1 + 4 * 64, 8 + c * 8, ref oyc.slot, nyc.slot);
                    DrawBuffYm2609.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
                else
                {
                    DrawBuffYm2609.Volume(screen, 288 + 1, 8 + c * 8, 0, ref oyc.volumeL, nyc.volumeL);
                    DrawBuffYm2609.KeyBoard(screen, 33, 8 + c * 8, ref oyc.note, nyc.note);
                    DrawBuffYm2609.ChYm2608(screen, c, ref oyc.mask, nyc.mask);
                    DrawBuffYm2609.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + c * 8, ref oyc.freq, nyc.freq);
                }
            }

            // PSG (channels 18-29).
            for (int c = 0; c < 12; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 18];
                MDChipParams.Channel nyc = newParam.channels[c + 18];

                DrawBuffYm2609.Volume(screen, 288 + 1, 8 + (c + 18) * 8, 1, ref oyc.volumeL, nyc.volume * ((nyc.pan & 0x2) != 0 ? 1 : 0));
                DrawBuffYm2609.Volume(screen, 288 + 1, 8 + (c + 18) * 8, 2, ref oyc.volumeR, nyc.volume * ((nyc.pan & 0x1) != 0 ? 1 : 0));
                DrawBuffYm2609.KeyBoard(screen, 33, (c + 18) * 8 + 8, ref oyc.note, nyc.note);
                DrawBuffYm2609.Pan(screen, 25, 8 + (c + 18) * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                DrawBuffYm2609.TnOpna(screen, 64, 2, c + 18, ref oyc.tn, nyc.tn, ref oyc.tntp, 0);

                DrawBuffYm2609.ChYm2608(screen, c + 18, ref oyc.mask, nyc.mask);
                DrawBuffYm2609.Font4Hex16Bit(screen, 1 + 4 * 68, 8 + (c + 18) * 8, ref oyc.freq, nyc.freq);

                DrawBuffYm2609.Font4Ym2609Duty(screen,
                    1 + 4 * 92 + (c % 6) * 4 * 14 + (((c / 3) & 1) == 0 ? 0 : 4 * 16),
                    8 * 21 + (c / 6) * 8 * 6,
                    0, ref oyc.bank, nyc.bank);

                if (nyc.bank < 0) continue;
                if (nyc.bank < 10)
                {
                    DrawBuffYm2609.WaveFormYm2609Preset(screen,
                        1 + 4 * 97 + (c % 6) * 4 * 14 + (((c / 3) & 1) == 0 ? 0 : 4 * 16),
                        20 * 8 + (c / 6) * 8 * 6,
                        ref oyc.volumeRR, nyc.bank);
                    continue;
                }

                DrawBuffYm2609.WaveFormYm2609User(screen,
                    1 + 4 * 97 + (c % 6) * 4 * 14 + (((c / 3) & 1) == 0 ? 0 : 4 * 16),
                    23 * 8 + (c / 6) * 8 * 6,
                    ref oyc.PSGWave, nyc.PSGWave);
            }

            for (int ch = 0; ch < 4; ch++)
            {
                DrawBuffYm2609.Nfrq(screen, 143 + (ch % 2) * 58, 40 + (ch / 2) * 12, ref oldParam.nfrq[ch], newParam.nfrq[ch]);
                DrawBuffYm2609.Efrq(screen, 143 + (ch % 2) * 58, 42 + (ch / 2) * 12, ref oldParam.efrq[ch], newParam.efrq[ch]);
                DrawBuffYm2609.Etype(screen, 143 + (ch % 2) * 58, 44 + (ch / 2) * 12, ref oldParam.etype[ch], newParam.etype[ch]);
            }

            // RHYTHM (30-35) + ADPCM-A (39-44) - drawn in the same 6-column loop as the
            // original (which drew ADPCM-A right under rhythm using its own 36-41 numbering;
            // here that's channels 39-44, see this file's header).
            for (int c = 0; c < 6; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 30];
                MDChipParams.Channel nyc = newParam.channels[c + 30];
                DrawBuffYm2609.VolumeYm2609Rhythm(screen, 4 * 107 + 1 + c * 68, 8 * 32, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2609.VolumeYm2609Rhythm(screen, 4 * 107 + 1 + c * 68, 8 * 32 + 4, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYm2609.PanYm2609Rhythm(screen, 4 * 105 + 1 + c * 68, 8 * 32, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                DrawBuffYm2609.Font4Int2(screen, 4 * 103 + 1 + c * 68, 8 * 32, ref oyc.volumeRL, nyc.volumeRL);

                oyc = oldParam.channels[c + 39];
                nyc = newParam.channels[c + 39];
                DrawBuffYm2609.VolumeYm2609Rhythm(screen, 4 * 107 + 1 + c * 68, 8 * 33, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2609.VolumeYm2609Rhythm(screen, 4 * 107 + 1 + c * 68, 8 * 33 + 4, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYm2609.PanType6(screen, 4 * 105 + 1 + c * 68, 8 * 33, ref oyc.pan, nyc.pan, 0);
                DrawBuffYm2609.Font4Int2(screen, 4 * 103 + 1 + c * 68, 8 * 33, ref oyc.volumeRL, nyc.volumeRL);
            }

            // ADPCM012 (36-38).
            for (int c = 0; c < 3; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c + 36];
                MDChipParams.Channel nyc = newParam.channels[c + 36];

                DrawBuffYm2609.Volume(screen, 289, (31 + c) * 8, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffYm2609.Volume(screen, 289, (31 + c) * 8, 2, ref oyc.volumeR, nyc.volumeR);
                DrawBuffYm2609.PanType5(screen, 25, (31 + c) * 8, ref oyc.panL, nyc.panL, 0);
                DrawBuffYm2609.PanType5(screen, 29, (31 + c) * 8, ref oyc.panR, nyc.panR, 0);
                DrawBuffYm2609.KeyBoard(screen, 33, (31 + c) * 8, ref oyc.note, nyc.note);
                DrawBuffYm2609.Font4Hex16Bit(screen, 1 + 4 * 68, (31 + c) * 8, ref oyc.freq, nyc.freq);
            }

            DrawBuffYm2609.Font4Hex12Bit(screen, 230 * 4 + 1, 38 * 4, ref oldParam.timerA, newParam.timerA);
            DrawBuffYm2609.Font4HexByte(screen, 230 * 4 + 1, 40 * 4, ref oldParam.timerB, newParam.timerB);
            DrawBuffYm2609.LfoSw(screen, 229 * 4 + 1, 42 * 4, ref oldParam.lfoSw[0], newParam.lfoSw[0]);
            DrawBuffYm2609.LfoFrq(screen, 229 * 4 + 1, 44 * 4, ref oldParam.lfoFrq[0], newParam.lfoFrq[0]);
            DrawBuffYm2609.LfoSw(screen, 229 * 4 + 1, 46 * 4, ref oldParam.lfoSw[1], newParam.lfoSw[1]);
            DrawBuffYm2609.LfoFrq(screen, 229 * 4 + 1, 48 * 4, ref oldParam.lfoFrq[1], newParam.lfoFrq[1]);
            DrawBuffYm2609.Font4Int3(screen, 229 * 4 + 1, 50 * 4, ref oldParam.rhythmTotalLevel[0], newParam.rhythmTotalLevel[0]);
            DrawBuffYm2609.Font4Int3(screen, 229 * 4 + 1, 52 * 4, ref oldParam.rhythmTotalLevel[1], newParam.rhythmTotalLevel[1]);
            DrawBuffYm2609.Font4Int3(screen, 229 * 4 + 1, 54 * 4, ref oldParam.adpcmLevel[0], newParam.adpcmLevel[0]);
            DrawBuffYm2609.Font4Int3(screen, 229 * 4 + 1, 56 * 4, ref oldParam.adpcmLevel[1], newParam.adpcmLevel[1]);
            DrawBuffYm2609.Font4Int3(screen, 229 * 4 + 1, 58 * 4, ref oldParam.adpcmLevel[2], newParam.adpcmLevel[2]);
            DrawBuffYm2609.DrawNesSw(screen, 220 * 4 + 1, 62 * 4, ref oldParam.eqLowSw, newParam.eqLowSw);
            DrawBuffYm2609.Font4Int3(screen, 222 * 4 + 1, 62 * 4, ref oldParam.eqLow[0], newParam.eqLow[0]);
            DrawBuffYm2609.Font4Int3(screen, 226 * 4 + 1, 62 * 4, ref oldParam.eqLow[1], newParam.eqLow[1]);
            DrawBuffYm2609.Font4Int3(screen, 230 * 4 + 1, 62 * 4, ref oldParam.eqLow[2], newParam.eqLow[2]);
            DrawBuffYm2609.DrawNesSw(screen, 220 * 4 + 1, 64 * 4, ref oldParam.eqMidSw, newParam.eqMidSw);
            DrawBuffYm2609.Font4Int3(screen, 222 * 4 + 1, 64 * 4, ref oldParam.eqMid[0], newParam.eqMid[0]);
            DrawBuffYm2609.Font4Int3(screen, 226 * 4 + 1, 64 * 4, ref oldParam.eqMid[1], newParam.eqMid[1]);
            DrawBuffYm2609.Font4Int3(screen, 230 * 4 + 1, 64 * 4, ref oldParam.eqMid[2], newParam.eqMid[2]);
            DrawBuffYm2609.DrawNesSw(screen, 220 * 4 + 1, 66 * 4, ref oldParam.eqHiSw, newParam.eqHiSw);
            DrawBuffYm2609.Font4Int3(screen, 222 * 4 + 1, 66 * 4, ref oldParam.eqHi[0], newParam.eqHi[0]);
            DrawBuffYm2609.Font4Int3(screen, 226 * 4 + 1, 66 * 4, ref oldParam.eqHi[1], newParam.eqHi[1]);
            DrawBuffYm2609.Font4Int3(screen, 230 * 4 + 1, 66 * 4, ref oldParam.eqHi[2], newParam.eqHi[2]);

            screen.Present();
        }
    }
}
