// Port of MDPlayer/MDPlayerx64/form/KB/PCM/frmMpcmX68k.cs - the X68000's MPCM (ADPCM/PCM)
// sample-player visualizer used by the ZMS and MND (mndrv) sequenced-music formats. 16
// channels, one 8px row per channel, each showing a channel badge, a raw pan icon, a
// keyboard/note readout, hex readouts of the sample pointer/size/start/end/count/pitch and
// PCM type byte, a text readout of the sample rate/format, and independent L/R volume bars.
//
// ARCHITECTURAL EXCEPTION: unlike every other chip ported so far (all of which read state via
// ChipRegister.GetXxxRegister(chipId)), frmMpcmX68k.cs does NOT go through Audio/ChipRegister
// at all. It instead reads driver-internal state directly off whichever sequencer driver is
// currently playing, via GetMPCMInstance casting Audio.DriverVirtual to ZMS or mndrv and
// pulling their public mpcmSt/mpcm/mpcmpp fields. This port's AudioShim.Audio.DriverVirtual is
// a dead stub (never wired to the live MusicEngineSession), so the equivalent live reference
// here is MusicEngineSession.Driver (a MDPlayer.baseDriver) - passed into this visualizer's
// constructor directly instead of a ChipRegister. There is also no meaningful clock value to
// take (the original never reads one either), so this constructor's shape is deliberately
// (baseDriver driver) rather than the standard (ChipRegister chipRegister, uint clockHz) used
// by every other XxxVisualizer.cs in this port - a genuine, documented exception justified by
// the original's genuinely different data-access architecture.
//
// This port's MusicEngine.cs (LoadMnd/LoadZms) always sets driver.mpcmtype = 1 and constructs
// a MDSound.mpcmpp instance - it never wires the raw MDSound.mpcmX68k model. That means in
// actual playback here, `mpcm` (the mpcmX68k branch) will always be null and only the mpcmpp
// branch is ever live - but both branches are preserved below for fidelity to the original,
// exactly as frmMpcmX68k.cs itself carries both.
//
// No enmInstrumentType.MpcmX68k entry exists in this port (or the original) - only .mpcmpp -
// so MainWindow.axaml.cs wires this visualizer's presence off enmInstrumentType.mpcmpp.
using MDPlayer;

namespace MDPlayer.UI.Visualizer
{
    public sealed class MpcmX68kVisualizer
    {
        private readonly PixelScreen screen;
        private readonly MDPlayer.baseDriver driver;
        private const int ChipID = 0;

        private readonly MDChipParams.MPCMX68k newParam = new();
        private readonly MDChipParams.MPCMX68k oldParam = new();

        public PixelScreen Screen => screen;

        public MpcmX68kVisualizer(MDPlayer.baseDriver driver)
        {
            this.driver = driver;

            DrawBuffMpcmX68k.LoadSprites();
            SpriteAtlas bg = SpriteAtlas.Load("planeMpcmX68k");

            screen = new PixelScreen();
            screen.Init(bg.Width, bg.Height, zoom: 2);
            screen.DrawIntArray(0, 0, bg.Pixels, bg.Width, 0, 0, bg.Width, bg.Height);
            screen.Present();

            for (int ch = 0; ch < 16; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                nyc.adr[5] = 4;
                nyc.adr[6] = 0x10000;
                nyc.pan = 3;
                nyc.volume = 0xff;
                nyc.volumeL = System.Math.Min(System.Math.Max((int)(nyc.volume / 13.0), 0), 19) * ((nyc.pan & 2) != 0 ? 1 : 0);
                nyc.volumeR = System.Math.Min(System.Math.Max((int)(nyc.volume / 13.0), 0), 19) * ((nyc.pan & 1) != 0 ? 1 : 0);
                nyc.note = -1;
            }
        }

        // frmMpcmX68k.cs:106 GetMPCMInstance - casts the live driver to ZMS or mndrv (this
        // port's equivalent of Audio.DriverVirtual) and pulls their public mpcmSt/mpcm/mpcmpp
        // fields directly.
        private void GetMpcmInstance(out MDPlayer.Driver.ZMS.ZMS.MPCMSt[] mpcmSt, out MDSound.mpcmX68k mpcm, out MDSound.mpcmpp mpcmpp)
        {
            mpcmSt = null;
            mpcm = null;
            mpcmpp = null;

            if (driver is MDPlayer.Driver.ZMS.ZMS zms)
            {
                mpcmSt = zms.mpcmSt;
                mpcm = zms.mpcm;
                mpcmpp = zms.mpcmpp;
            }
            else if (driver is MDPlayer.Driver.MNDRV.mndrv mnd)
            {
                mpcmSt = mnd.mpcmSt;
                mpcm = mnd.mpcm;
                mpcmpp = mnd.mpcmpp;
            }
        }

        // frmMpcmX68k.cs:225 searchMPCMX68kNote - preserves the non-global-minimum
        // "keeps overwriting on every hz > a" quirk and the n-5+12 return offset verbatim.
        private int SearchMpcmX68kNote(MDSound.mpcmX68k mpcm, uint pitch, float base_)
        {
            int freq = (int)(pitch * base_);
            int clock = (int)mpcm.m[ChipID].rate;
            int hz = (int)(clock / (0x10000 / (double)freq));

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(4000.0 * Tables.pcmMulTbl[i % 12 + 12] * System.Math.Pow(2, (i / 12 - 3 + 2)));
                if (hz > a)
                {
                    n = i;
                }
            }
            return n - 5 + 12;
        }

        // frmMpcmX68k.cs:250 searchMPCMppNote - same quirk/offset, mpcmpp variant.
        private int SearchMpcmPpNote(MDSound.mpcmpp mpcm, uint pitch, float base_)
        {
            int freq = (int)(pitch * base_);
            int clock = (int)mpcm.m[ChipID].rate;
            int hz = (int)(clock / (0x10000 / (double)freq));

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(4000.0 * Tables.pcmMulTbl[i % 12 + 12] * System.Math.Pow(2, (i / 12 - 3 + 2)));
                if (hz > a)
                {
                    n = i;
                }
            }
            return n - 5 + 12;
        }

        // frmMpcmX68k.cs:129 screenChangeParams.
        public void ScreenChangeParams()
        {
            GetMpcmInstance(out MDPlayer.Driver.ZMS.ZMS.MPCMSt[] mpcmSt, out MDSound.mpcmX68k mpcm, out MDSound.mpcmpp mpcmpp);
            if (mpcmSt == null || (mpcm == null && mpcmpp == null)) return;

            for (int ch = 0; ch < mpcmSt.Length; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                if (mpcmSt[ch].Keyon)
                {
                    nyc.adr[0] = (uint)mpcmSt[ch].adrs_ptr;
                    nyc.adr[1] = mpcmSt[ch].size;
                    nyc.adr[2] = mpcmSt[ch].start;
                    nyc.adr[3] = mpcmSt[ch].end;
                    nyc.adr[4] = mpcmSt[ch].count;
                    nyc.adr[5] = (uint)mpcmSt[ch].frq;
                    if (mpcm != null)
                    {
                        nyc.adr[6] = mpcm.m[ChipID].work[ch].pitch;
                        nyc.sadr = mpcm.m[ChipID].work[ch].type;
                    }
                    if (mpcmpp != null)
                    {
                        nyc.adr[6] = mpcmpp.m[ChipID].work[ch].pitch;
                        nyc.sadr = mpcmpp.m[ChipID].work[ch].type;
                    }

                    if (mpcmSt[ch].pan < 0x80)
                    {
                        // 1:left 3:center 2:right -> 1:right 3:center 2:left
                        nyc.pan = mpcmSt[ch].pan;
                        nyc.pan = (nyc.pan == 1 ? 2 : (nyc.pan == 2 ? 1 : 3));
                    }
                    else
                    {
                        // 1:right 3:center 2:left
                        int pan = mpcmSt[ch].pan - 0x80;
                        if (pan >= 0 && pan <= 31)
                        {
                            nyc.pan = 2;
                        }
                        else if (pan >= 32 && pan <= 95)
                        {
                            nyc.pan = 3;
                        }
                        else if (pan >= 96 && pan <= 127)
                        {
                            nyc.pan = 1;
                        }
                    }

                    nyc.volume = mpcmSt[ch].volume;
                    nyc.volumeL = System.Math.Min(System.Math.Max(mpcmSt[ch].volume, 1), 19) * ((nyc.pan & 2) != 0 ? 1 : 0);
                    nyc.volumeR = System.Math.Min(System.Math.Max(mpcmSt[ch].volume, 1), 19) * ((nyc.pan & 1) != 0 ? 1 : 0);

                    int orig = 440 << 6;
                    if (mpcmSt[ch].orig != 0)
                    {
                        orig = mpcmSt[ch].orig << 6;
                    }
                    int dnote = (short)mpcmSt[ch].pitch;
                    uint pitch = 0x1_0000;

                    if (orig > 0x1fc0)
                    {
                        pitch = 0x1_0000;
                        if (mpcm != null) nyc.note = SearchMpcmX68kNote(mpcm, pitch, mpcm.m[ChipID].work[ch].base_);
                        if (mpcmpp != null) nyc.note = SearchMpcmPpNote(mpcmpp, pitch, mpcmpp.m[ChipID].work[ch].base_);
                    }
                    else
                    {
                        nyc.note = dnote / 64 - 24;
                    }

                    mpcmSt[ch].Keyon = false;
                    mpcmSt[ch].Keyoff = false;
                }
                else if (mpcmSt[ch].Keyoff)
                {
                    mpcmSt[ch].Keyon = false;
                    mpcmSt[ch].Keyoff = false;
                    nyc.note = -1;
                }
                else
                {
                    if (nyc.volumeL > 0) nyc.volumeL--;
                    if (nyc.volumeR > 0) nyc.volumeR--;
                }
            }
        }

        // frmMpcmX68k.cs:275 screenDrawParams.
        public void ScreenDrawParams()
        {
            for (int c = 0; c < 16; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuffMpcmX68k.ChMpcmX68k(screen, c, ref oyc.mask, nyc.mask);
                DrawBuffMpcmX68k.Pan(screen, 32, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, 0);
                DrawBuffMpcmX68k.KeyBoardXYFX(screen, 40, 142 * 4, 8 + c * 8, ref oyc.note, nyc.note);

                int x = 67;
                DrawBuffMpcmX68k.Font4Hex32Bit(screen, (x + 0) * 4, (c + 1) * 8, ref oyc.adr[0], nyc.adr[0]); // ptr
                DrawBuffMpcmX68k.Font4Hex32Bit(screen, (x + 9) * 4, (c + 1) * 8, ref oyc.adr[1], nyc.adr[1]); // size
                DrawBuffMpcmX68k.Font4Hex32Bit(screen, (x + 18) * 4, (c + 1) * 8, ref oyc.adr[2], nyc.adr[2]); // start
                DrawBuffMpcmX68k.Font4Hex32Bit(screen, (x + 27) * 4, (c + 1) * 8, ref oyc.adr[3], nyc.adr[3]); // end
                DrawBuffMpcmX68k.Font4Hex32Bit(screen, (x + 36) * 4, (c + 1) * 8, ref oyc.adr[4], nyc.adr[4]); // count
                if (oyc.adr[5] != nyc.adr[5])
                {
                    DrawBuffMpcmX68k.DrawFont4(screen, (x + 44) * 4, (c + 1) * 8, 1, FrqStr[System.Math.Min(System.Math.Max((int)nyc.adr[5], 0), FrqStr.Length - 1)]);
                    oyc.adr[5] = nyc.adr[5];
                }
                DrawBuffMpcmX68k.Font4Hex32Bit(screen, (x + 54) * 4, (c + 1) * 8, ref oyc.adr[6], nyc.adr[6]); // pitch
                DrawBuffMpcmX68k.Font4HexByte(screen, (x + 63) * 4, (c + 1) * 8, ref oyc.sadr, nyc.sadr); // type

                DrawBuffMpcmX68k.Volume(screen, (x + 65) * 4, c * 8 + 8, 1, ref oyc.volumeL, nyc.volumeL);
                DrawBuffMpcmX68k.Volume(screen, (x + 65) * 4, c * 8 + 8, 2, ref oyc.volumeR, nyc.volumeR);
            }

            screen.Present();
        }

        // frmMpcmX68k.cs:312 frqStr - verbatim 43-entry sample rate/format string table.
        private static readonly string[] FrqStr = new string[]
        {
            "AD 3906HZ", "AD 5208HZ", "AD 7812HZ", "AD10416HZ", "AD15625HZ",
            "WM20800HZ", "BM31200HZ", "WMTHROUGH", "WM15625HZ", "WM16000HZ",
            "WM22050HZ", "WM24000HZ", "WM32000HZ", "WM44100HZ", "WM48000HZ",
            "WMVARIABL", "BM15625HZ", "BM16000HZ", "BM22050HZ", "BM24000HZ",
            "BM32000HZ", "BM44100HZ", "BM48000HZ", "BMVARIABL", "WS15625HZ",
            "WS16000HZ", "WS22050HZ", "WS24000HZ", "WS32000HZ", "WS44100HZ",
            "WS48000HZ", "WSVARIABL", "BS15625HZ", "BS16000HZ", "BS22050HZ",
            "BS24000HZ", "BS32000HZ", "BS44100HZ", "BS48000HZ", "BSVARIABL",
            "ADVARIABL", "WMVARIABL", "         ",
        };
    }
}
