#if X64
using MDPlayerx64.Properties;
#else
using MDPlayer.Properties;
#endif
namespace MDPlayer.form
{
    partial class frmYM2612MIDI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmYM2612MIDI));
            pbScreen = new PictureBox();
            cmsMIDIKBD = new ContextMenuStrip(components);
            ctsmiCopy = new ToolStripMenuItem();
            ctsmiPaste = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)pbScreen).BeginInit();
            cmsMIDIKBD.SuspendLayout();
            SuspendLayout();
            // 
            // pbScreen
            // 
            pbScreen.Location = new Point(0, 0);
            pbScreen.Margin = new Padding(4, 4, 4, 4);
            pbScreen.Name = "pbScreen";
            pbScreen.Size = new Size(373, 230);
            pbScreen.TabIndex = 0;
            pbScreen.TabStop = false;
            pbScreen.MouseClick += pbScreen_MouseClick;
            // 
            // cmsMIDIKBD
            // 
            cmsMIDIKBD.Items.AddRange(new ToolStripItem[] { ctsmiCopy, ctsmiPaste });
            cmsMIDIKBD.Name = "cmsMIDIKBD";
            cmsMIDIKBD.Size = new Size(131, 48);
            // 
            // ctsmiCopy
            // 
            ctsmiCopy.Name = "ctsmiCopy";
            ctsmiCopy.Size = new Size(130, 22);
            ctsmiCopy.Text = "コピー(&C)";
            ctsmiCopy.Click += ctsmiCopy_Click;
            // 
            // ctsmiPaste
            // 
            ctsmiPaste.Name = "ctsmiPaste";
            ctsmiPaste.Size = new Size(130, 22);
            ctsmiPaste.Text = "貼り付け(&P)";
            ctsmiPaste.Click += ctsmiPaste_Click;
            // 
            // frmYM2612MIDI
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(373, 230);
            Controls.Add(pbScreen);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5, 5, 5, 5);
            MaximizeBox = false;
            Name = "frmYM2612MIDI";
            Text = "MIDI(YM2612)";
            FormClosed += frmYM2612MIDI_FormClosed;
            Load += frmYM2612MIDI_Load;
            KeyDown += frmYM2612MIDI_KeyDown;
            ((System.ComponentModel.ISupportInitialize)pbScreen).EndInit();
            cmsMIDIKBD.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        public System.Windows.Forms.PictureBox pbScreen;
        private System.Windows.Forms.ContextMenuStrip cmsMIDIKBD;
        private System.Windows.Forms.ToolStripMenuItem ctsmiCopy;
        private System.Windows.Forms.ToolStripMenuItem ctsmiPaste;
    }
}