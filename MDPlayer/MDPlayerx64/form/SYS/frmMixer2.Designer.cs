#if X64
using MDPlayerx64.Properties;
#else
using MDPlayer.Properties;
#endif
namespace MDPlayer.form
{
    partial class frmMixer2
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMixer2));
            pbScreen = new PictureBox();
            ctxtMenu = new ContextMenuStrip(components);
            tsmiLoadDriverBalance = new ToolStripMenuItem();
            tsmiLoadSongBalance = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            tsmiSaveDriverBalance = new ToolStripMenuItem();
            tsmiSaveSongBalance = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)pbScreen).BeginInit();
            ctxtMenu.SuspendLayout();
            SuspendLayout();
            // 
            // pbScreen
            // 
            pbScreen.ContextMenuStrip = ctxtMenu;
            pbScreen.Location = new Point(0, 0);
            pbScreen.Margin = new Padding(4, 4, 4, 4);
            pbScreen.Name = "pbScreen";
            pbScreen.Size = new Size(373, 360);
            pbScreen.TabIndex = 0;
            pbScreen.TabStop = false;
            pbScreen.MouseClick += pbScreen_MouseClick;
            pbScreen.MouseDown += frmMixer2_MouseDown;
            pbScreen.MouseEnter += pbScreen_MouseEnter;
            pbScreen.MouseMove += frmMixer2_MouseMove;
            // 
            // ctxtMenu
            // 
            ctxtMenu.Items.AddRange(new ToolStripItem[] { tsmiLoadDriverBalance, tsmiLoadSongBalance, toolStripSeparator1, tsmiSaveDriverBalance, tsmiSaveSongBalance });
            ctxtMenu.Name = "ctxtMenu";
            ctxtMenu.Size = new Size(224, 98);
            // 
            // tsmiLoadDriverBalance
            // 
            tsmiLoadDriverBalance.Enabled = false;
            tsmiLoadDriverBalance.Name = "tsmiLoadDriverBalance";
            tsmiLoadDriverBalance.Size = new Size(223, 22);
            tsmiLoadDriverBalance.Text = "読込　ドライバーミキサーバランス";
            tsmiLoadDriverBalance.Click += tsmiLoadDriverBalance_Click;
            // 
            // tsmiLoadSongBalance
            // 
            tsmiLoadSongBalance.Enabled = false;
            tsmiLoadSongBalance.Name = "tsmiLoadSongBalance";
            tsmiLoadSongBalance.Size = new Size(223, 22);
            tsmiLoadSongBalance.Text = "読込　ソングミキサーバランス";
            tsmiLoadSongBalance.Click += tsmiLoadSongBalance_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(220, 6);
            // 
            // tsmiSaveDriverBalance
            // 
            tsmiSaveDriverBalance.Name = "tsmiSaveDriverBalance";
            tsmiSaveDriverBalance.Size = new Size(223, 22);
            tsmiSaveDriverBalance.Text = "保存　ドライバーミキサーバランス";
            tsmiSaveDriverBalance.Click += tsmiSaveDriverBalance_Click;
            // 
            // tsmiSaveSongBalance
            // 
            tsmiSaveSongBalance.Name = "tsmiSaveSongBalance";
            tsmiSaveSongBalance.Size = new Size(223, 22);
            tsmiSaveSongBalance.Text = "保存　ソングミキサーバランス";
            tsmiSaveSongBalance.Click += tsmiSaveSongBalance_Click;
            // 
            // frmMixer2
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(373, 360);
            Controls.Add(pbScreen);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 4, 4, 4);
            MaximizeBox = false;
            Name = "frmMixer2";
            Text = "Mixer";
            FormClosed += frmMixer2_FormClosed;
            Load += frmMixer2_Load;
            KeyDown += frmMixer2_KeyDown;
            MouseDown += frmMixer2_MouseDown;
            MouseMove += frmMixer2_MouseMove;
            ((System.ComponentModel.ISupportInitialize)pbScreen).EndInit();
            ctxtMenu.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        public System.Windows.Forms.PictureBox pbScreen;
        private System.Windows.Forms.ContextMenuStrip ctxtMenu;
        private System.Windows.Forms.ToolStripMenuItem tsmiSaveDriverBalance;
        private System.Windows.Forms.ToolStripMenuItem tsmiSaveSongBalance;
        private System.Windows.Forms.ToolStripMenuItem tsmiLoadDriverBalance;
        private System.Windows.Forms.ToolStripMenuItem tsmiLoadSongBalance;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    }
}