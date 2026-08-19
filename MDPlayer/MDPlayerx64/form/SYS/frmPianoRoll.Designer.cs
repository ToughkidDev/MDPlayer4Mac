namespace MDPlayer.form
{
    partial class frmPianoRoll
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
            pbScreen = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)pbScreen).BeginInit();
            SuspendLayout();
            // 
            // pbScreen
            // 
            pbScreen.BackColor = Color.Black;
            pbScreen.Location = new Point(0, 0);
            pbScreen.Name = "pbScreen";
            pbScreen.Size = new Size(1024, 384);
            pbScreen.TabIndex = 0;
            pbScreen.TabStop = false;
            // 
            // frmPianoRoll
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.ControlDarkDark;
            ClientSize = new Size(1024, 385);
            Controls.Add(pbScreen);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MaximumSize = new Size(1040, 424);
            MinimizeBox = false;
            MinimumSize = new Size(1040, 424);
            Name = "frmPianoRoll";
            Text = "Piano Roll";
            FormClosed += frmPianoRoll_FormClosed;
            Load += frmPianoRoll_Load;
            Shown += frmPianoRoll_Shown;
            Resize += frmPianoRoll_Resize;
            ((System.ComponentModel.ISupportInitialize)pbScreen).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox pbScreen;
    }
}