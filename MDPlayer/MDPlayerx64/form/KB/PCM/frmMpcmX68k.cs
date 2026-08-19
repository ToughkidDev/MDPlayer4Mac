#if X64
using MDPlayer.Driver.MNDRV;
using MDPlayer.Driver.MXDRV;
using MDPlayer.Driver.ZMS;
using MDPlayer.Driver.ZMS.nise68;
using MDPlayerx64;
using MDSound;
using static MDPlayer.Driver.ZMS.ZMS;
using static MDSound.mpcmX68k;
#else
using MDPlayer.Properties;
#endif

namespace MDPlayer.form
{
    public partial class frmMpcmX68k : frmBase
    {
        public bool isClosed = false;
        public int x = -1;
        public int y = -1;
        private int frameSizeW = 0;
        private int frameSizeH = 0;
        private int chipID = 0;
        private int zoom = 1;

        private MDChipParams.MPCMX68k newParam = null;
        private MDChipParams.MPCMX68k oldParam = new MDChipParams.MPCMX68k();
        private FrameBuffer frameBuffer = new FrameBuffer();

        public frmMpcmX68k(frmMain frm, int chipID, int zoom, MDChipParams.MPCMX68k newParam) : base(frm)
        {
            this.chipID = chipID;
            this.zoom = zoom;

            InitializeComponent();

            this.newParam = newParam;
            frameBuffer.Add(pbScreen, ResMng.ImgDic["planeMpcmX68k"], null, zoom);
            DrawBuff.screenInitMPCMX68k(frameBuffer);

            for (int ch = 0; ch < 16; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                nyc.adr[5] = 4;
                nyc.adr[6] = 0x10000;
                nyc.pan = 3;
                nyc.volume = 0xff;
                nyc.volumeL = Math.Min(Math.Max((int)(nyc.volume / 13.0), 0), 19) * ((nyc.pan & 2) != 0 ? 1 : 0);
                nyc.volumeR = Math.Min(Math.Max((int)(nyc.volume / 13.0), 0), 19) * ((nyc.pan & 1) != 0 ? 1 : 0);
                nyc.note = -1;
            }

            update();
        }

        public void update()
        {
            frameBuffer.Refresh(null);
        }

        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }

        private void frmMpcmX68k_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                parent.setting.location.PosMPCMX68k[chipID] = Location;
            }
            else
            {
                parent.setting.location.PosMPCMX68k[chipID] = RestoreBounds.Location;
            }
            isClosed = true;
        }

        private void frmMpcmX68k_Load(object sender, EventArgs e)
        {
            this.Location = new Point(x, y);

            frameSizeW = this.Width - this.ClientSize.Width;
            frameSizeH = this.Height - this.ClientSize.Height;

            changeZoom();
        }

        public void changeZoom()
        {
            this.MaximumSize = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeMpcmX68k"].Width * zoom, frameSizeH + ResMng.ImgDic["planeMpcmX68k"].Height * zoom);
            this.MinimumSize = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeMpcmX68k"].Width * zoom, frameSizeH + ResMng.ImgDic["planeMpcmX68k"].Height * zoom);
            this.Size = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeMpcmX68k"].Width * zoom, frameSizeH + ResMng.ImgDic["planeMpcmX68k"].Height * zoom);
            frmMpcmX68k_Resize(null, null);

        }

        private void frmMpcmX68k_Resize(object sender, EventArgs e)
        {

        }

        private void GetMPCMInstance(out ZMS.MPCMSt[] MPCMSt, out mpcmX68k mpcm, out mpcmpp mpcmpp)
        {
            MPCMSt = null;
            mpcm = null;
            mpcmpp = null;

            if (Audio.DriverVirtual is ZMS)
            {
                ZMS zms = Audio.DriverVirtual as ZMS;
                MPCMSt = zms.mpcmSt;
                mpcm = zms.mpcm;
                mpcmpp = zms.mpcmpp;
            }
            else if (Audio.DriverVirtual is mndrv)
            {
                mndrv mnd = Audio.DriverVirtual as mndrv;
                MPCMSt = mnd.mpcmSt;
                mpcm = mnd.mpcm;
                mpcmpp = mnd.mpcmpp;
            }
            return;
        }

        public void screenChangeParams()
        {
            GetMPCMInstance(out MPCMSt[] MPCMSt, out mpcmX68k mpcm, out mpcmpp mpcmpp);
            if (mpcm == null && mpcmpp == null) return;

            for (int ch = 0; ch < MPCMSt.Length; ch++)
            {
                MDChipParams.Channel nyc = newParam.channels[ch];
                if (MPCMSt[ch].Keyon)
                {
                    nyc.adr[0] = (uint)MPCMSt[ch].adrs_ptr;
                    nyc.adr[1] = (uint)MPCMSt[ch].size;
                    nyc.adr[2] = (uint)MPCMSt[ch].start;
                    nyc.adr[3] = (uint)MPCMSt[ch].end;
                    nyc.adr[4] = (uint)MPCMSt[ch].count;
                    nyc.adr[5] = (uint)MPCMSt[ch].frq;
                    if (mpcm != null)
                    {
                        nyc.adr[6] = (uint)mpcm.m[chipID].work[ch].pitch;
                        nyc.sadr = mpcm.m[chipID].work[ch].type;// MPCMSt[ch].type;
                    }
                    if (mpcmpp != null)
                    {
                        nyc.adr[6] = (uint)mpcmpp.m[chipID].work[ch].pitch;
                        nyc.sadr = mpcmpp.m[chipID].work[ch].type;// MPCMSt[ch].type;
                    }
                    if (MPCMSt[ch].pan < 0x80)
                    {
                        //1:left 3:center 2:right
                        // ->
                        //1:right 3:center 2:left 
                        nyc.pan = MPCMSt[ch].pan;
                        nyc.pan = (nyc.pan == 1 ? 2 : (nyc.pan == 2 ? 1 : 3));
                    }
                    else
                    {
                        //1:right 3:center 2:left 
                        int pan = MPCMSt[ch].pan - 0x80;
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

                    nyc.volume = (int)MPCMSt[ch].volume;
                    nyc.volumeL = Math.Min(Math.Max((int)(MPCMSt[ch].volume ), 1), 19) * ((nyc.pan & 2) != 0 ? 1 : 0);
                    nyc.volumeR = Math.Min(Math.Max((int)(MPCMSt[ch].volume ), 1), 19) * ((nyc.pan & 1) != 0 ? 1 : 0);

                    int orig = 440 << 6;
                    if (MPCMSt[ch].orig != 0)
                    {
                        orig = MPCMSt[ch].orig << 6;
                    }
                    int doct = 0;
                    int dnote = (Int16)MPCMSt[ch].pitch;
                    uint pitch = 0x1_0000;

                    if (orig > 0x1fc0)//MPCMSt[ch].orig>0x7f
                    {
                        //pitch = (uint)(0x10000 * MPCMSt[ch].base_);
                        pitch = 0x1_0000;
                        if (mpcm != null) nyc.note = searchMPCMX68kNote(mpcm, pitch, mpcm.m[chipID].work[ch].base_);// * mpcm.m[0].rate);
                        if (mpcmpp != null) nyc.note = searchMPCMppNote(mpcmpp, pitch, mpcmpp.m[chipID].work[ch].base_);// * mpcm.m[0].rate);
                    }
                    else
                    {
                        nyc.note = dnote / 64 - 24;
                    }

                    MPCMSt[ch].Keyon = false;
                    MPCMSt[ch].Keyoff = false;
                }
                else if (MPCMSt[ch].Keyoff)
                {
                    MPCMSt[ch].Keyon = false;
                    MPCMSt[ch].Keyoff = false;
                    nyc.note = -1;
                }
                else
                {
                    if (nyc.volumeL > 0) nyc.volumeL--;
                    if (nyc.volumeR > 0) nyc.volumeR--;
                    //if (nyc.volumeL == 0 && nyc.volumeR == 0) nyc.note = -1;
                }
            }
        }

        private int searchMPCMX68kNote(mpcmX68k mpcm,uint pitch,float base_)
        {
            int freq = (int)(pitch * base_);
            int clock = (int)mpcm.m[chipID].rate;
            int hz = (int)(clock / (0x10000 / (double)freq));
            double m = double.MaxValue;

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(
                    4000.0
                    * Tables.pcmMulTbl[i % 12 + 12]
                    * Math.Pow(2, (i / 12 - 3 + 2))
                    );

                if (hz > a)
                {
                    m = a;
                    n = i;
                }
            }
            return n-5+12;
        }

        private int searchMPCMppNote(mpcmpp mpcm, uint pitch, float base_)
        {
            int freq = (int)(pitch * base_);
            int clock = (int)mpcm.m[chipID].rate;
            int hz = (int)(clock / (0x10000 / (double)freq));
            double m = double.MaxValue;

            int n = 0;
            for (int i = 0; i < 12 * 8; i++)
            {
                int a = (int)(
                    4000.0
                    * Tables.pcmMulTbl[i % 12 + 12]
                    * Math.Pow(2, (i / 12 - 3 + 2))
                    );

                if (hz > a)
                {
                    m = a;
                    n = i;
                }
            }
            return n - 5 + 12;
        }

        public void screenDrawParams()
        {
            MDChipParams.MPCMX68k ost = oldParam;
            MDChipParams.MPCMX68k nst = newParam;
            int tp = 0;

            for (int c = 0; c < 16; c++)
            {
                MDChipParams.Channel oyc = oldParam.channels[c];
                MDChipParams.Channel nyc = newParam.channels[c];

                DrawBuff.ChMPCMX68k(frameBuffer, c, ref oyc.mask, nyc.mask, tp);

                DrawBuff.Pan(frameBuffer, 32, 8 + c * 8, ref oyc.pan, nyc.pan, ref oyc.pantp, tp);

                DrawBuff.KeyBoardXYFX(frameBuffer, 40, 142 * 4, 8 + c * 8, ref oyc.note, nyc.note, tp);

                int x = 67;
                DrawBuff.font4Hex32Bit(frameBuffer, (x + 0) * 4, (c + 1) * 8, 0, ref oyc.adr[0], nyc.adr[0]);//ptr
                DrawBuff.font4Hex32Bit(frameBuffer, (x + 9) * 4, (c + 1) * 8, 0, ref oyc.adr[1], nyc.adr[1]);//size
                DrawBuff.font4Hex32Bit(frameBuffer, (x + 18) * 4, (c + 1) * 8, 0, ref oyc.adr[2], nyc.adr[2]);//start
                DrawBuff.font4Hex32Bit(frameBuffer, (x + 27) * 4, (c + 1) * 8, 0, ref oyc.adr[3], nyc.adr[3]);//end
                DrawBuff.font4Hex32Bit(frameBuffer, (x + 36) * 4, (c + 1) * 8, 0, ref oyc.adr[4], nyc.adr[4]);//count
                if (oyc.adr[5] != nyc.adr[5])
                {
                    DrawBuff.drawFont4(frameBuffer, (x + 44) * 4, (c + 1) * 8, 1, frqStr[Math.Min(Math.Max(nyc.adr[5], 0), frqStr.Length)]);//frq
                    oyc.adr[5] = nyc.adr[5];
                }
                DrawBuff.font4Hex32Bit(frameBuffer, (x + 54) * 4, (c + 1) * 8, 0, ref oyc.adr[6], nyc.adr[6]);//pitch
                DrawBuff.font4HexByte(frameBuffer, (x + 63) * 4, (c + 1) * 8, 0, ref oyc.sadr, nyc.sadr);//type

                DrawBuff.Volume(frameBuffer, (x + 65) * 4, c * 8 + 8, 1, ref oyc.volumeL, nyc.volumeL, 0);
                DrawBuff.Volume(frameBuffer, (x + 65) * 4, c * 8 + 8, 2, ref oyc.volumeR, nyc.volumeR, 0);
            }

        }

        string[] frqStr = new string[]{
            "AD 3906HZ",//0
            "AD 5208HZ",//1
            "AD 7812HZ",//2
            "AD10416HZ",//3
            "AD15625HZ",//4
            "WM20800HZ",//5
            "BM31200HZ",//6
            "WMTHROUGH",//7
            "WM15625HZ",//8
            "WM16000HZ",//9
            "WM22050HZ",//10
            "WM24000HZ",//11
            "WM32000HZ",//12
            "WM44100HZ",//13
            "WM48000HZ",//14
            "WMVARIABL",//15
            "BM15625HZ",//16
            "BM16000HZ",//17
            "BM22050HZ",//18
            "BM24000HZ",//19
            "BM32000HZ",//20
            "BM44100HZ",//21
            "BM48000HZ",//22
            "BMVARIABL",//23
            "WS15625HZ",//24
            "WS16000HZ",//25
            "WS22050HZ",//26
            "WS24000HZ",//27
            "WS32000HZ",//28
            "WS44100HZ",//29
            "WS48000HZ",//30
            "WSVARIABL",//31
            "BS15625HZ",//32
            "BS16000HZ",//33
            "BS22050HZ",//34
            "BS24000HZ",//35
            "BS32000HZ",//36
            "BS44100HZ",//37
            "BS48000HZ",//38
            "BSVARIABL",//39
            "ADVARIABL",//40
            "WMVARIABL",//41
            "         ",//42
        };

        public void screenInit()
        {
            for (int ch = 0; ch < 16; ch++)
            {
                newParam.channels[ch].note = -1;
                for (int i = 0; i < 7; i++)
                {
                    newParam.channels[ch].adr[i] = 0;
                }
                newParam.channels[ch].adr[5] = 42;
                newParam.channels[ch].sadr = 0;
                newParam.channels[ch].pan = 3;
            }
        }

        private void pbScreen_MouseClick(object sender, MouseEventArgs e)
        {
            int px = e.Location.X / zoom;
            int py = e.Location.Y / zoom;

            //上部のラベル行の場合は何もしない
            if (py < 1 * 8)
            {
                //但しchをクリックした場合はマスク反転
                if (px < 8)
                {
                    for (int ch = 0; ch < 16; ch++)
                    {
                        if (newParam.channels[ch].mask == true)
                            parent.ResetChannelMask(EnmChip.MPCMX68k, chipID, ch);
                        else
                            parent.SetChannelMask(EnmChip.MPCMX68k, chipID, ch);
                    }
                }
                return;
            }

            //鍵盤
            if (py < 17 * 8)
            {
                int ch = (py / 8) - 1;
                if (ch < 0) return;

                if (e.Button == MouseButtons.Left)
                {
                    //マスク
                    parent.SetChannelMask(EnmChip.MPCMX68k, chipID, ch);
                    return;
                }

                //マスク解除
                for (ch = 0; ch < 16; ch++) parent.ResetChannelMask(EnmChip.MPCMX68k, chipID, ch);
                return;
            }

        }
    }
}
