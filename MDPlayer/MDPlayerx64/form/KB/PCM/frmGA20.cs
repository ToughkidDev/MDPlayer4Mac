using MDPlayerx64;
#if X64
#else
using MDPlayer.Properties;
#endif

namespace MDPlayer.form
{
    public partial class frmGA20 : frmBase
    {
        public bool isClosed = false;
        public int x = -1;
        public int y = -1;
        private int frameSizeW = 0;
        private int frameSizeH = 0;
        private int chipID = 0;
        private int zoom = 1;
        private MDChipParams.GA20 newParam = null;
        private MDChipParams.GA20 oldParam = new MDChipParams.GA20();
        private FrameBuffer frameBuffer = new FrameBuffer();

        public frmGA20(frmMain frm, int chipID, int zoom, MDChipParams.GA20 newParam, MDChipParams.GA20 oldParam) : base(frm)
        {
            InitializeComponent();

            parent = frm;
            this.chipID = chipID;
            this.zoom = zoom;
            this.newParam = newParam;
            this.oldParam = oldParam;

            frameBuffer.Add(pbScreen, ResMng.ImgDic["planeGA20"], null, zoom);
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

        private void frmGA20_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                parent.setting.location.PosGA20[chipID] = Location;
            }
            else
            {
                parent.setting.location.PosGA20[chipID] = RestoreBounds.Location;
            }
            isClosed = true;
        }

        private void frmGA20_Load(object sender, EventArgs e)
        {
            this.Location = new Point(x, y);

            frameSizeW = this.Width - this.ClientSize.Width;
            frameSizeH = this.Height - this.ClientSize.Height;

            changeZoom();
        }

        public void changeZoom()
        {
            this.MaximumSize = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeGA20"].Width * zoom, frameSizeH + ResMng.ImgDic["planeGA20"].Height * zoom);
            this.MinimumSize = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeGA20"].Width * zoom, frameSizeH + ResMng.ImgDic["planeGA20"].Height * zoom);
            this.Size = new System.Drawing.Size(frameSizeW + ResMng.ImgDic["planeGA20"].Width * zoom, frameSizeH + ResMng.ImgDic["planeGA20"].Height * zoom);
            frmGA20_Resize(null, null);

        }

        private void frmGA20_Resize(object sender, EventArgs e)
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
                    for (ch = 0; ch < 4; ch++)
                    {
                        if (newParam.channels[ch].mask == true)
                            parent.ResetChannelMask(EnmChip.GA20, chipID, ch);
                        else
                            parent.SetChannelMask(EnmChip.GA20, chipID, ch);
                    }
                }
                return;
            }

            ch = (py / 8) - 1;
            if (ch < 0) return;

            if (ch < 4)
            {
                if (e.Button == MouseButtons.Left)
                {
                    parent.SetChannelMask(EnmChip.GA20, chipID, ch);
                    return;
                }

                for (ch = 0; ch < 4; ch++) parent.ResetChannelMask(EnmChip.GA20, chipID, ch);
                return;

            }
        }

        public void screenInit()
        {
            bool GA20Type = false;// (chipID == 0) ? parent.setting.GA20Type.UseScci : parent.setting.GA20SType.UseScci;
            int tp = GA20Type ? 1 : 0;
            for (int ch = 0; ch < 32; ch++)
            {
                for (int ot = 0; ot < 12 * 8; ot++)
                {
                    int kx = Tables.kbl[(ot % 12) * 2] + ot / 12 * 28;
                    int kt = Tables.kbl[(ot % 12) * 2 + 1];
                    DrawBuff.drawKbn(frameBuffer, 32 + kx, ch * 8 + 8, kt, tp);
                }
                //DrawBuff.drawFont8(frameBuffer, 296, ch * 8 + 8, 1, "   ");
                DrawBuff.drawPanType2P(frameBuffer, 24, ch * 8 + 8, 0, tp);
                DrawBuff.ChC140_P(frameBuffer, 0, 8 + ch * 8, ch, false, tp);
                //DrawBuff.Volume(frameBuffer, ch, 1, ref d, 0, tp);
                //DrawBuff.Volume(frameBuffer, ch, 2, ref d, 0, tp);
            }
        }

        private int searchGA20Note(int freq)
        {
            int clock = Audio.ClockGA20 / 4;
            int hz = clock / (256 - freq);
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
            return n;
        }

        public void screenChangeParams()
        {
            MDSound.iremga20.ga20_state GA20Register = Audio.GetGA20State(chipID);
            bool[] GA20KeyOn=Audio.GetGA20KeyOn(chipID);

            if (GA20Register == null) return;
            ;

            for (int ch = 0; ch < 4; ch++)
            {

                newParam.channels[ch].freq = (int)GA20Register.regs[4 + (ch << 3)];
                newParam.channels[ch].sadr = (int)GA20Register.channel[ch].start;
                newParam.channels[ch].eadr = (int)GA20Register.channel[ch].end;
                newParam.channels[ch].ladr = (int)GA20Register.channel[ch].pos;
                newParam.channels[ch].volume = (int)GA20Register.regs[5 + (ch << 3)];
                newParam.channels[ch].note = searchGA20Note(newParam.channels[ch].freq);

                if (GA20KeyOn[ch])
                {
                    newParam.channels[ch].volumeL =Common.Range(
                        (int)(256 - GA20Register.regs[5 + (ch << 3)]) / 13
                        ,0,19);
                    GA20KeyOn[ch] = false;
                }
                else
                {
                    if (newParam.channels[ch].volumeL > 0) newParam.channels[ch].volumeL--;
                    else newParam.channels[ch].note = -1;
                }

            }
        }

        public void screenDrawParams()
        {
            MDChipParams.Channel oyc;
            MDChipParams.Channel nyc;

            for (int ch = 0; ch < 4; ch++)
            {
                oyc = oldParam.channels[ch];
                nyc = newParam.channels[ch];

                DrawBuff.font4Hex20Bit(frameBuffer, 4 * 65, ch * 8 + 8, 0, ref oyc.sadr, nyc.sadr);
                DrawBuff.font4Hex20Bit(frameBuffer, 4 * 71, ch * 8 + 8, 0, ref oyc.eadr, nyc.eadr);
                DrawBuff.font4Hex20Bit(frameBuffer, 4 * 77, ch * 8 + 8, 0, ref oyc.ladr, nyc.ladr);
                DrawBuff.font4HexByte(frameBuffer, 4 * 83, ch * 8 + 8, 0, ref oyc.freq, nyc.freq);
                DrawBuff.font4HexByte(frameBuffer, 4 * 86, ch * 8 + 8, 0, ref oyc.volume, nyc.volume);
                DrawBuff.KeyBoardToGA20(frameBuffer, ch, ref oyc.note, nyc.note, 0);
                DrawBuff.Volume(frameBuffer, 4 * 88, ch * 8 + 8, 0, ref oyc.volumeL, nyc.volumeL, 0);
                DrawBuff.ChC352(frameBuffer, ch, ref oyc.mask, nyc.mask, 0);
            }
        }

    }
}
