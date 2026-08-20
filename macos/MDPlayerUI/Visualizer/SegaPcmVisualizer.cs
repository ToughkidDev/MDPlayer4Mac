// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmSegaPCM.cs - Sega's SegaPCM 16-channel PCM
// sample player, used across many mid-80s Sega arcade boards (Hang-On/OutRun/Space Harrier
// and others). This is the last remaining PCM-family chip in this port's roadmap. 16
// sample-playback channels, one 8px row per channel, each showing a keyboard/note readout,
// independent L/R volume LED bars, a byte-packed 2-tile pan icon, and a channel badge.
//
// Data source: reads chipRegister.pcmRegisterSEGAPCM[chipId] (a 0x200-byte flat register
// image) and chipRegister.pcmKeyOnSEGAPCM[chipId] (a 16-entry live key-on flag array)
// directly - both are already public ChipRegister fields (Audio.GetSEGAPCMRegister/
// GetSEGAPCMKeyOn just forward to the same fields on Windows), so no new getter was needed,
// same shape as C140Visualizer's pcmRegisterC140/pcmKeyOnC140.
//
// Note computation reuses Common.searchSegaPCMNote directly (not duplicated) since the
// original itself calls this already-shared helper - matching the established pattern from
// QSound/YM2413/YM3526/YM3812/Y8950/YMF262/YMF278B/VRC7 and the earlier K051649 exception.
//
// No clock handling needed - unlike C140/C352/GA20/K053260/K054539's frequency-vs-clock
// division, searchSegaPCMNote compares a pre-divided "ml" ratio (raw delta-time byte / 256.0)
// directly against a pcmMulTbl-based table, and performs a genuine, honest global-minimum
// search (matching MegaCD/RF5C164/Rf5c68, not the "keeps overwriting on every freq > a" quirk
// shared by C140/C352/GA20/K053260/K054539).
//
// Genuinely-preserved original quirk: a `voldelay` frame counter (starts at 3, decremented
// every ScreenChangeParams call, reset to 5 once it goes negative) gates how often a
// non-keyed-on channel's volume decays by 1 - volumes only step down roughly every 6th call
// rather than every call, unlike every other PCM-family chip's per-call decay. Ported
// verbatim rather than "fixed" into a per-call decay, per this port's philosophy of
// preserving original behavior/quirks.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class SegaPcmVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private const int ChipID = 0;

        private readonly MDChipParams.SegaPcm newParam = new();
        private readonly MDChipParams.SegaPcm oldParam = new();

        private int voldelay = 3;

        public PixelScreen Screen => screen;

        public SegaPcmVisualizer(ChipRegister chipRegister, uint clockHz)
        {
            this.chipRegister = chipRegister;
            _ = clockHz; // frmSegaPCM.cs's screenChangeParams never reads a clock value.

            DrawBuffSegaPCM.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeSEGAPCM");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmSegaPCM.cs:154 screenChangeParams.
        public void ScreenChangeParams()
        {
            byte[] segapcmReg = chipRegister.pcmRegisterSEGAPCM[ChipID];
            bool[] segapcmKeyOn = chipRegister.pcmKeyOnSEGAPCM[ChipID];
            if (segapcmReg == null) return;

            for (int ch = 0; ch < 16; ch++)
            {
                MDChipParams.Channel channel = newParam.channels[ch];

                int l = segapcmReg[ch * 8 + 2] & 0x7f;
                int r = segapcmReg[ch * 8 + 3] & 0x7f;
                int dt = segapcmReg[ch * 8 + 7];
                double ml = dt / 256.0;

                if (segapcmKeyOn[ch])
                {
                    channel.note = Common.searchSegaPCMNote(ml);
                    channel.volumeL = Math.Min(Math.Max((l * 1) >> 1, 2), 19);
                    channel.volumeR = Math.Min(Math.Max((r * 1) >> 1, 2), 19);
                }
                else
                {
                    if (voldelay == 0)
                    {
                        channel.volumeL -= channel.volumeL > 0 ? 1 : 0;
                        channel.volumeR -= channel.volumeR > 0 ? 1 : 0;
                    }

                    if (channel.volumeL == 0 && channel.volumeR == 0)
                    {
                        channel.note = -1;
                    }
                }

                channel.pan = ((l >> 3) & 0xf) | (((r >> 3) & 0xf) << 4);

                segapcmKeyOn[ch] = false;
            }

            voldelay--;
            if (voldelay < 0) voldelay = 5;
        }

        // frmSegaPCM.cs:243 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 16; c++)
            {
                MDChipParams.Channel orc = oldParam.channels[c];
                MDChipParams.Channel nrc = newParam.channels[c];

                DrawBuffSegaPCM.Volume(screen, 256, 8 + c * 8, 1, ref orc.volumeL, nrc.volumeL);
                DrawBuffSegaPCM.Volume(screen, 256, 8 + c * 8, 2, ref orc.volumeR, nrc.volumeR);
                DrawBuffSegaPCM.KeyBoard(screen, c, ref orc.note, nrc.note);
                DrawBuffSegaPCM.PanType2(screen, c, ref orc.pan, nrc.pan);

                DrawBuffSegaPCM.ChSegaPcm(screen, c, ref orc.mask, nrc.mask);
            }

            screen.Present();
        }
    }
}
