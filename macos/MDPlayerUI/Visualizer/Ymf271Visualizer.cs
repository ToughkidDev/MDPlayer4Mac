// Port of MDPlayer/MDPlayerx64/form/KB/OPX/frmYMF271.cs - the YMF271 (OPX) channel
// visualizer. Unlike every OPN-family chip ported so far, YMF271 has no FM/SSG/rhythm
// section split - it's 48 identical FM+PCM "wavetable synthesis" slots in a flat list, each
// independently either an FM voice (envelope params AR/DR/SR/RR/SL/TL/KS/ML/DT/waveform/
// feedback/accon/algorithm) or a PCM sample-playback voice (start/end/loop address, sample
// rate/bit-depth/source-note/source-bank), plus a per-slot LFO. Slots are grouped 4-at-a-
// time (12 groups); one "sync mode" icon is drawn per group, not per slot. No channel-mask
// badge exists for this chip at all.
//
// Data source: reads chipRegister.GetYMF271Register(chipID), which returns the emulation
// core's own already-decoded MDSound.ymf271.YMF271Chip struct tree (48 YMF271Slot entries
// plus 12 YMF271Group entries) - the original Visualizer reads this decoded state directly
// rather than re-deriving it from raw register bytes, and this port does the same (see
// ChipRegister.cs's GetYMF271Register, added alongside this file since no prior chip needed
// it).
//
// slotTbl reorders the 48 raw slot indices into "4 operators of channel 0, then 4 operators
// of channel 1, ..." display order (frmYMF271.cs:23-39) - transcribed verbatim.
//
// Deliberate simplification: `tp` hardcoded to 0, same as every other DrawBuffXxx.cs in
// this port. This port also skips replicating frmYMF271.cs's screenInit's explicit
// blank-piano-key/blank-pan pre-draw loop (draws nothing but placeholder icons at their
// eventual final positions) - like every other chip in this port, the first real
// ScreenDrawParams call naturally draws everything via the normal dirty-diff path since
// MDChipParams.Channel's default field values (-1 etc.) differ from any real decoded state.
using System;
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class Ymf271Visualizer : IChannelVisualizer
    {
        private readonly PixelScreen screen;
        private readonly ChipRegister chipRegister;
        private readonly int ChipID;

        private readonly MDChipParams.YMF271 newParam = new();
        private readonly MDChipParams.YMF271 oldParam = new();

        public PixelScreen Screen => screen;

        // frmYMF271.cs:23-39 - raw slot index -> display order (4 operators per channel,
        // 12 channels).
        private static readonly int[] SlotTbl =
        {
            0, 24, 12, 36,
            1, 25, 13, 37,
            2, 26, 14, 38,
            3, 27, 15, 39,

            4, 28, 16, 40,
            5, 29, 17, 41,
            6, 30, 18, 42,
            7, 31, 19, 43,

            8, 32, 20, 44,
            9, 33, 21, 45,
            10, 34, 22, 46,
            11, 35, 23, 47,
        };

        public Ymf271Visualizer(ChipRegister chipRegister, uint clockHz, int chipID = 0)
        {
            ChipID = chipID;
            this.chipRegister = chipRegister;
            _ = clockHz; // frmYMF271.cs's screenChangeParams never reads a clock value.

            DrawBuffYmf271.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeYMF271");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();
        }

        // frmYMF271.cs:127 screenChangeParams.
        public void ScreenChangeParams()
        {
            MDSound.ymf271.YMF271Chip reg = chipRegister.GetYMF271Register(ChipID);
            if (reg == null) return;

            for (int i = 0; i < 48; i++)
            {
                int slot = SlotTbl[i];

                MDChipParams.Channel nrc = newParam.channels[slot];
                MDSound.ymf271.YMF271Slot slt = reg.slots[slot];

                nrc.volumeL = Math.Min(Math.Max(slt.volume * slt.ch0_level >> 23, 0), 19);
                nrc.volumeR = Math.Min(Math.Max(slt.volume * slt.ch1_level >> 23, 0), 19);
                nrc.pan = (slt.ch1_level << 4) | (slt.ch0_level & 0xf);
                nrc.pantp = (slt.ch3_level & 0xf0) | ((slt.ch2_level >> 4) & 0xf);
                nrc.inst[0] = slt.ar; // AR (&0x1f)
                nrc.inst[1] = slt.decay1rate; // DR (&0x1f)
                nrc.inst[2] = slt.decay2rate; // SR (&0x1f)
                nrc.inst[3] = slt.relrate; // RR (&0xf)
                nrc.inst[4] = slt.decay1lvl; // SL (&0xf)
                nrc.inst[5] = slt.tl; // TL (&0x7f)
                nrc.inst[6] = slt.keyscale; // KS (&0x7)
                nrc.inst[7] = slt.multiple; // ML (&0xf)
                nrc.inst[8] = slt.detune; // DT (&0x7)
                nrc.inst[9] = slt.waveform; // waveform (&0x7)
                nrc.inst[10] = slt.feedback; // feedback (&0x7)
                nrc.inst[11] = slt.accon; // accon (&0x80)
                nrc.inst[12] = slt.algorithm; // algorithm (&0x0f)

                nrc.inst[13] = slt.block; // block (&0x0f)
                nrc.inst[14] = (int)slt.fns; // fns (&0x0fff)

                nrc.inst[15] = (int)slt.startaddr; // (&0xffffff)
                nrc.inst[16] = (int)slt.endaddr; // (&0xffffff)
                nrc.inst[17] = (int)slt.loopaddr; // (&0xffffff)

                nrc.inst[18] = slt.fs; // (&0x3)
                nrc.inst[19] = slt.bits == 12 ? 1 : 0; // (&0x4)
                nrc.inst[20] = slt.srcnote; // (&0x3)
                nrc.inst[21] = slt.srcb; // (&0x7)

                nrc.inst[22] = slt.lfoFreq; // (&0xff)
                nrc.inst[23] = slt.lfowave; // (&0x3)
                nrc.inst[24] = slt.pms; // (&0x7)
                nrc.inst[25] = slt.ams; // (&0x3)

                if (slt.active != 0)
                {
                    nrc.volumeL = Math.Min(Math.Max(slt.volume * slt.ch0_level >> 23, 0), 19);
                    nrc.volumeR = Math.Min(Math.Max(slt.volume * slt.ch1_level >> 23, 0), 19);
                    nrc.note = Common.searchSSGNote(nrc.inst[14]) + (((nrc.inst[13] + 8) & 0xf) - 11) * 12 - 7;
                }
                else
                {
                    nrc.volumeL += nrc.volumeL > 0 ? -1 : 0;
                    nrc.volumeR += nrc.volumeR > 0 ? -1 : 0;
                    nrc.note = -1;
                }

                if (i % 4 == 0)
                {
                    nrc.tn = reg.groups[i / 4].sync;
                }
            }
        }

        // frmYMF271.cs:196 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int i = 0; i < 48; i++)
            {
                int slot = SlotTbl[i];

                MDChipParams.Channel orc = oldParam.channels[slot];
                MDChipParams.Channel nrc = newParam.channels[slot];

                DrawBuffYmf271.Volume(screen, 273, 8 + i * 8, 1, ref orc.volumeL, nrc.volumeL);
                DrawBuffYmf271.Volume(screen, 273, 12 + i * 8, 1, ref orc.volumeR, nrc.volumeR);
                DrawBuffYmf271.Font4Int2(screen, 25, 8 + i * 8, ref orc.echo, slot + 1); // slot number
                DrawBuffYmf271.PanType2(screen, 33, 8 + i * 8, ref orc.pan, nrc.pan);
                DrawBuffYmf271.PanType2(screen, 41, 8 + i * 8, ref orc.pantp, nrc.pantp);

                DrawBuffYmf271.KeyBoard(screen, 49, 8 + i * 8, ref orc.note, nrc.note);

                DrawBuffYmf271.Font4Int2(screen, 357, 8 + i * 8, ref orc.inst[0], nrc.inst[0]); // AR
                DrawBuffYmf271.Font4Int2(screen, 365, 8 + i * 8, ref orc.inst[1], nrc.inst[1]); // DR
                DrawBuffYmf271.Font4Int2(screen, 373, 8 + i * 8, ref orc.inst[2], nrc.inst[2]); // SR
                DrawBuffYmf271.Font4Int2(screen, 381, 8 + i * 8, ref orc.inst[3], nrc.inst[3]); // RR
                DrawBuffYmf271.Font4Int2(screen, 389, 8 + i * 8, ref orc.inst[4], nrc.inst[4]); // SL
                DrawBuffYmf271.Font4Int3(screen, 397, 8 + i * 8, ref orc.inst[5], nrc.inst[5]); // TL
                DrawBuffYmf271.Font4Int1(screen, 413, 8 + i * 8, ref orc.inst[6], nrc.inst[6]); // KS
                DrawBuffYmf271.Font4Int2(screen, 417, 8 + i * 8, ref orc.inst[7], nrc.inst[7]); // ML
                DrawBuffYmf271.Font4Int1(screen, 429, 8 + i * 8, ref orc.inst[8], nrc.inst[8]); // DT
                DrawBuffYmf271.Font4Int1(screen, 437, 8 + i * 8, ref orc.inst[9], nrc.inst[9]); // WF
                DrawBuffYmf271.Font4Int1(screen, 445, 8 + i * 8, ref orc.inst[10], nrc.inst[10]); // FB
                DrawBuffYmf271.Font4Int1(screen, 449, 8 + i * 8, ref orc.inst[11], nrc.inst[11]); // accon
                DrawBuffYmf271.Font4Int2(screen, 453, 8 + i * 8, ref orc.inst[12], nrc.inst[12]); // algorithm
                DrawBuffYmf271.Font4Int2(screen, 465, 8 + i * 8, ref orc.inst[13], nrc.inst[13]); // block
                DrawBuffYmf271.Font4Hex12Bit(screen, 477, 8 + i * 8, ref orc.inst[14], nrc.inst[14]); // fns
                DrawBuffYmf271.Font4Hex24Bit(screen, 497, 8 + i * 8, ref orc.inst[15], nrc.inst[15]); // startaddr
                DrawBuffYmf271.Font4Hex24Bit(screen, 525, 8 + i * 8, ref orc.inst[16], nrc.inst[16]); // endaddr
                DrawBuffYmf271.Font4Hex24Bit(screen, 553, 8 + i * 8, ref orc.inst[17], nrc.inst[17]); // loopaddr
                DrawBuffYmf271.Font4Int1(screen, 581, 8 + i * 8, ref orc.inst[18], nrc.inst[18]); // fs
                DrawBuffYmf271.Font4Int1(screen, 585, 8 + i * 8, ref orc.inst[19], nrc.inst[19]); // bits
                DrawBuffYmf271.Font4Int1(screen, 589, 8 + i * 8, ref orc.inst[20], nrc.inst[20]); // srcnote
                DrawBuffYmf271.Font4Int1(screen, 593, 8 + i * 8, ref orc.inst[21], nrc.inst[21]); // srcb

                DrawBuffYmf271.Font4Int3(screen, 601, 8 + i * 8, ref orc.inst[22], nrc.inst[22]); // lfofreq
                DrawBuffYmf271.Font4Int1(screen, 617, 8 + i * 8, ref orc.inst[23], nrc.inst[23]); // lfowave
                DrawBuffYmf271.Font4Int1(screen, 621, 8 + i * 8, ref orc.inst[24], nrc.inst[24]); // pms
                DrawBuffYmf271.Font4Int1(screen, 625, 8 + i * 8, ref orc.inst[25], nrc.inst[25]); // ams

                if (i % 4 == 0)
                {
                    DrawBuffYmf271.OpxOP(screen, 17, 8 + i * 8, ref orc.tn, nrc.tn & 3); // sync
                }
            }

            screen.Present();
        }
    }
}
