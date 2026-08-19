using MDPlayer.Driver.MNDRV;
using MDPlayerx64;
#if X64
#else
using MDPlayer.Properties;
#endif

namespace MDPlayer.form
{
    public partial class frmK054539 : frmBase
    {
        public bool isClosed = false;
        public int x = -1;
        public int y = -1;
        private int frameSizeW = 0;
        private int frameSizeH = 0;
        private int chipID = 0;
        private int zoom = 1;
        private MDChipParams.K054539 newParam = null;
        private MDChipParams.K054539 oldParam = new MDChipParams.K054539();
        private FrameBuffer frameBuffer = new FrameBuffer();

        public frmK054539(frmMain frm, int chipID, int zoom, MDChipParams.K054539 newParam, MDChipParams.K054539 oldParam) : base(frm)
        {
            InitializeComponent();

            parent = frm;
            this.chipID = chipID;
            this.zoom = zoom;
            this.newParam = newParam;
            this.oldParam = oldParam;

            frameBuffer.Add(pbScreen, ResMng.ImgDic["planeK054539"], null, zoom);
            screenInit();
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

        private void frmK054539_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                parent.setting.location.PosK054539[chipID] = Location;
            }
            else
            {
                parent.setting.location.PosK054539[chipID] = RestoreBounds.Location;
            }
            isClosed = true;
        }

        private void frmK054539_Load(object sender, EventArgs e)
        {
            this.Location = new Point(x, y);

            frameSizeW = this.Width - this.ClientSize.Width;
            frameSizeH = this.Height - this.ClientSize.Height;

            changeZoom();
        }

        public void changeZoom()
        {
            this.MaximumSize = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeK054539"].Width * zoom, frameSizeH + ResMng.ImgDic["planeK054539"].Height * zoom);
            this.MinimumSize = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeK054539"].Width * zoom, frameSizeH + ResMng.ImgDic["planeK054539"].Height * zoom);
            this.Size = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeK054539"].Width * zoom, frameSizeH + ResMng.ImgDic["planeK054539"].Height * zoom);
            frmK054539_Resize(null, null);

        }

        private void frmK054539_Resize(object sender, EventArgs e)
        {

        }


        private void pbScreen_MouseClick(object sender, MouseEventArgs e)
        {
            int px = e.Location.X / zoom;
            int py = e.Location.Y / zoom;
            int ch;
            //上部のラベル行の場合は何もしない
            if (py < 1 * 8)
            {
                //但しchをクリックした場合はマスク反転
                if (px < 8)
                {
                    for (ch = 0; ch < 8; ch++)
                    {
                        if (newParam.channels[ch].mask == true)
                            parent.ResetChannelMask(EnmChip.K054539, chipID, ch);
                        else
                            parent.SetChannelMask(EnmChip.K054539, chipID, ch);
                    }
                }
                return;
            }

            ch = (py / 8) - 1;
            if (ch < 0) return;

            if (ch < 8)
            {
                if (e.Button == MouseButtons.Left)
                {
                    parent.SetChannelMask(EnmChip.K054539, chipID, ch);
                    return;
                }

                for (ch = 0; ch < 8; ch++) parent.ResetChannelMask(EnmChip.K054539, chipID, ch);
                return;

            }
        }

        public void screenInit()
        {
            bool K054539Type = false;// (chipID == 0) ? parent.setting.K054539Type.UseScci : parent.setting.K054539SType.UseScci;
            int tp = K054539Type ? 1 : 0;
            for (int ch = 0; ch < 8; ch++)
            {
                for (int ot = 0; ot < 12 * 8; ot++)
                {
                    int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                    int kt = Tables.kbl[(ot % 12) * 2 + 1];
                    DrawBuff.drawKbn(frameBuffer, 32 + kx, ch * 8 + 8, kt, tp);
                }
                //DrawBuff.drawFont8(frameBuffer, 296, ch * 8 + 8, 1, "   ");
                DrawBuff.drawPanType2P(frameBuffer, 24, ch * 8 + 8, 0, tp);
                //DrawBuff.ChC140_P(frameBuffer, 0, 8 + ch * 8, ch, false, tp);
                //DrawBuff.Volume(frameBuffer, ch, 1, ref d, 0, tp);
                //DrawBuff.Volume(frameBuffer, ch, 2, ref d, 0, tp);
            }
        }

        private int searchK054539Note(int freq)
        {
            int clock = Audio.ClockK054539;
            if (clock >= 1000000) clock /= 384;
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
            return n+1;
        }

        private static int[] pantbl = new int[] {
        0 *5+4 , 1 *5+4 ,1 *5+4 , 2 *5+4 , 2 *5+4 , 3 *5+4 , 3 *5+4 ,
        4 *5+4,
        4 *5+3 , 4 *5+3 ,4 *5+2 , 4 *5+2 , 4 *5+1 , 4 *5+1 , 4 *5+0 ,
        };


        public void screenChangeParams()
        {
            MDSound.K054539.k054539_state K054539Register = Audio.GetK054539State(chipID);
            //bool[] K054539KeyOn=Audio.GetK054539KeyOn(chipID);

            if (K054539Register == null) return;
            ;

            for (int ch = 0; ch < 8; ch++)
            {

                byte pan = K054539Register.regs[0x20 * ch + 0x05];//pan
                if (pan >= 0x81 && pan <= 0x8f)
                    pan -= 0x81;
                else if (pan >= 0x11 && pan <= 0x1f)
                    pan -= 0x11;
                else
                    pan = 0x18 - 0x11;
                newParam.channels[ch].pan = pantbl[pan];

                newParam.channels[ch].sadr = (int)(K054539Register.regs[0x20 * ch + 0x0c]//start adr
                    + (K054539Register.regs[0x20 * ch + 0x0d] << 8)
                    + (K054539Register.regs[0x20 * ch + 0x0e] << 16));
                newParam.channels[ch].eadr = (int)(K054539Register.regs[0x20 * ch + 0x08]//loop adr
                    + (K054539Register.regs[0x20 * ch + 0x09] << 8)
                    + (K054539Register.regs[0x20 * ch + 0x0a] << 16));
                newParam.channels[ch].echo = (int)(K054539Register.regs[0x20 * ch + 0x00]//pitch
                    + (K054539Register.regs[0x20 * ch + 0x01] << 8)
                    + (K054539Register.regs[0x20 * ch + 0x02] << 16));
                newParam.channels[ch].freq = (int)(K054539Register.regs[0x20 * ch + 0x06]//rev delay
                    + (K054539Register.regs[0x20 * ch + 0x07] << 8));
                newParam.channels[ch].kf = (int)(K054539Register.regs[0x20 * ch + 0x04]);//rev
                newParam.channels[ch].bank = (int)((K054539Register.regs[0x200 + 0x2 * ch + 0] & 0xc) >> 2);//PCM type
                newParam.channels[ch].loopFlg = ((K054539Register.regs[0x200 + 0x2 * ch + 1] & 0x1) != 0);//loop
                newParam.channels[ch].ex = ((K054539Register.regs[0x200 + 0x2 * ch + 0] & 0x20) != 0);//reverse
                newParam.channels[ch].volume = (int)(K054539Register.regs[0x20 * ch + 0x03]);//volume
                byte vol = (byte)(0x40-Common.Range(newParam.channels[ch].volume, 0, 0x40));
                newParam.channels[ch].dda = ((K054539Register.regs[0x214] & (1 << ch)) != 0);//keyon
                newParam.channels[ch].noise = ((K054539Register.regs[0x215] & (1 << ch)) != 0);//keyoff


                if ((K054539Register.regs[0x22c]&(1<<ch)) !=0)
                {
                    newParam.channels[ch].volumeL = (byte)Common.Range(vol * 19 * (newParam.channels[ch].pan / 5) / 4 / 0x40, 0, 19);
                    newParam.channels[ch].volumeR = (byte)Common.Range(vol * 19 * (newParam.channels[ch].pan % 5) / 4 / 0x40, 0, 19);
                    newParam.channels[ch].note = searchK054539Note(newParam.channels[ch].echo);
                }
                else
                {
                    if (newParam.channels[ch].volumeL > 0) newParam.channels[ch].volumeL--;
                    if (newParam.channels[ch].volumeR > 0) newParam.channels[ch].volumeR--;
                    newParam.channels[ch].note = -1;
                }

            }
        }

        public void screenDrawParams()
        {
            MDChipParams.Channel oyc;
            MDChipParams.Channel nyc;

            for (int ch = 0; ch < 8; ch++)
            {
                oyc = oldParam.channels[ch];
                nyc = newParam.channels[ch];

                DrawBuff.font4Hex24Bit(frameBuffer, 4 * 9, ch * 8 + 8 * 10, 0, ref oyc.sadr, nyc.sadr);//start adr
                DrawBuff.font4Hex24Bit(frameBuffer, 4 * 17, ch * 8 + 8 * 10, 0, ref oyc.eadr, nyc.eadr);//loopadr
                DrawBuff.font4Hex24Bit(frameBuffer, 4 * 25, ch * 8 + 8 * 10, 0, ref oyc.echo, nyc.echo);//pitch
                DrawBuff.font4Hex16Bit(frameBuffer, 4 * 33, ch * 8 + 8 * 10, 0, ref oyc.freq, nyc.freq);//rev.delay
                DrawBuff.font4HexByte(frameBuffer, 4 * 39, ch * 8 + 8 * 10, 0, ref oyc.kf, nyc.kf);//rev
                DrawBuff.font4HexByte(frameBuffer, 4 * 43, ch * 8 + 8 * 10, 0, ref oyc.bank, nyc.bank);//pcm type
                DrawBuff.drawNESSw(frameBuffer, 4 * 46, ch * 8 + 8 * 10, ref oyc.loopFlg, nyc.loopFlg);//loop
                DrawBuff.drawNESSw(frameBuffer, 4 * 48, ch * 8 + 8 * 10, ref oyc.ex, nyc.ex);//reverse
                DrawBuff.font4HexByte(frameBuffer, 4 * 51, ch * 8 + 8 * 10, 0, ref oyc.volume, nyc.volume);//volume
                DrawBuff.drawNESSw(frameBuffer, 4 * 54, ch * 8 + 8 * 10, ref oyc.dda, nyc.dda);//keyon
                DrawBuff.drawNESSw(frameBuffer, 4 * 56, ch * 8 + 8 * 10, ref oyc.noise, nyc.noise);//keyoff

                DrawBuff.PanType4(frameBuffer, 4 * 6, ch * 8 + 8 * 1, ref oyc.pan, nyc.pan,0);//keyoff
                DrawBuff.KeyBoard(frameBuffer, ch, ref oyc.note, nyc.note, 0);
                DrawBuff.Volume(frameBuffer, 4 * 64, ch * 8 + 8, 1, ref oyc.volumeL, nyc.volumeL, 0);
                DrawBuff.Volume(frameBuffer, 4 * 64, ch * 8 + 12, 1, ref oyc.volumeR, nyc.volumeR, 0);
                DrawBuff.ChC352(frameBuffer, ch, ref oyc.mask, nyc.mask, 0);
            }
        }

    }
}
