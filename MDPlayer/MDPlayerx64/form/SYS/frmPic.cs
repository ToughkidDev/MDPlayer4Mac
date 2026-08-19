using MDPlayer;
using MDPlayer.form;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MDPlayerx64.form.SYS
{
    public partial class frmPic : Form
    {
        public frmMain parent = null;
        public Setting setting = null;
        public bool isClosed = false;
        public int x = -1;
        public int y = -1;
        public int w = -1;
        public int h = -1;

        public frmPic(frmMain frm)
        {
            parent = frm;
            InitializeComponent();
        }

        private void frmPic_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                parent.setting.location.PPic = Location;
                parent.setting.location.SPic = new Point(Size.Width, Size.Height);

            }
            else
            {
                parent.setting.location.PPic = RestoreBounds.Location;
                parent.setting.location.SPic = new Point(RestoreBounds.Size.Width, RestoreBounds.Size.Height);
            }

            isClosed = true;

        }

        private void frmPic_Load(object sender, EventArgs e)
        {

            this.Location = new Point(x, y);
            this.Size = new Size(w, h);
        }

        protected override void WndProc(ref Message m)
        {
            if (parent != null)
            {
                parent.WindowsMessage(ref m);
            }

            base.WndProc(ref m);
        }

    }
}
