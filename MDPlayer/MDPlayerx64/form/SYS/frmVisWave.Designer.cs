#if X64
using MDPlayerx64;
using MDPlayerx64.Properties;
#else
using MDPlayer.Properties;
#endif

namespace MDPlayer.form
{
    partial class frmVisWave
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmVisWave));
            pictureBox1 = new PictureBox();
            timer1 = new System.Windows.Forms.Timer(components);
            toolStripContainer1 = new ToolStripContainer();
            toolStrip1 = new ToolStrip();
            tsbHeight1 = new ToolStripButton();
            tsbHeight2 = new ToolStripButton();
            tsbHeight3 = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            tsbDispType1 = new ToolStripButton();
            tsbDispType2 = new ToolStripButton();
            tsbFFT = new ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            toolStripContainer1.ContentPanel.SuspendLayout();
            toolStripContainer1.TopToolStripPanel.SuspendLayout();
            toolStripContainer1.SuspendLayout();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Margin = new Padding(4);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(261, 226);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // timer1
            // 
            timer1.Enabled = true;
            timer1.Interval = 10;
            timer1.Tick += timer1_Tick;
            // 
            // toolStripContainer1
            // 
            // 
            // toolStripContainer1.ContentPanel
            // 
            toolStripContainer1.ContentPanel.Controls.Add(pictureBox1);
            toolStripContainer1.ContentPanel.Margin = new Padding(4);
            toolStripContainer1.ContentPanel.Size = new Size(261, 226);
            toolStripContainer1.Dock = DockStyle.Fill;
            toolStripContainer1.Location = new Point(0, 0);
            toolStripContainer1.Margin = new Padding(4);
            toolStripContainer1.Name = "toolStripContainer1";
            toolStripContainer1.Size = new Size(261, 251);
            toolStripContainer1.TabIndex = 1;
            toolStripContainer1.Text = "toolStripContainer1";
            // 
            // toolStripContainer1.TopToolStripPanel
            // 
            toolStripContainer1.TopToolStripPanel.Controls.Add(toolStrip1);
            // 
            // toolStrip1
            // 
            toolStrip1.Dock = DockStyle.None;
            toolStrip1.Items.AddRange(new ToolStripItem[] { tsbHeight1, tsbHeight2, tsbHeight3, toolStripSeparator1, tsbDispType1, tsbDispType2, tsbFFT });
            toolStrip1.Location = new Point(3, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(156, 25);
            toolStrip1.TabIndex = 0;
            // 
            // tsbHeight1
            // 
            tsbHeight1.BackgroundImage = Resources.vHeight1;
            tsbHeight1.BackgroundImageLayout = ImageLayout.Center;
            tsbHeight1.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsbHeight1.ImageTransparentColor = Color.Magenta;
            tsbHeight1.Name = "tsbHeight1";
            tsbHeight1.Size = new Size(23, 22);
            tsbHeight1.Text = "Height x 0.3";
            tsbHeight1.Click += tsbHeight1_Click;
            // 
            // tsbHeight2
            // 
            tsbHeight2.BackgroundImage = Resources.vHeight2;
            tsbHeight2.BackgroundImageLayout = ImageLayout.Center;
            tsbHeight2.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsbHeight2.ImageTransparentColor = Color.Magenta;
            tsbHeight2.Name = "tsbHeight2";
            tsbHeight2.Size = new Size(23, 22);
            tsbHeight2.Text = "Height x 1.0";
            tsbHeight2.Click += tsbHeight2_Click;
            // 
            // tsbHeight3
            // 
            tsbHeight3.BackgroundImage = Resources.vHeight3;
            tsbHeight3.BackgroundImageLayout = ImageLayout.Center;
            tsbHeight3.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsbHeight3.ImageTransparentColor = Color.Magenta;
            tsbHeight3.Name = "tsbHeight3";
            tsbHeight3.Size = new Size(23, 22);
            tsbHeight3.Text = "Height x 3.0";
            tsbHeight3.Click += tsbHeight3_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(6, 25);
            // 
            // tsbDispType1
            // 
            tsbDispType1.BackgroundImage = Resources.vType1;
            tsbDispType1.BackgroundImageLayout = ImageLayout.Center;
            tsbDispType1.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsbDispType1.ImageTransparentColor = Color.Magenta;
            tsbDispType1.Name = "tsbDispType1";
            tsbDispType1.Size = new Size(23, 22);
            tsbDispType1.Text = "type 1";
            tsbDispType1.Click += tsbDispType1_Click;
            // 
            // tsbDispType2
            // 
            tsbDispType2.BackgroundImage = Resources.vType2;
            tsbDispType2.BackgroundImageLayout = ImageLayout.Center;
            tsbDispType2.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsbDispType2.ImageTransparentColor = Color.Magenta;
            tsbDispType2.Name = "tsbDispType2";
            tsbDispType2.Size = new Size(23, 22);
            tsbDispType2.Text = "type 2";
            tsbDispType2.Click += tsbDispType2_Click;
            // 
            // tsbFFT
            // 
            tsbFFT.BackgroundImage = Resources.vType3;
            tsbFFT.BackgroundImageLayout = ImageLayout.Center;
            tsbFFT.CheckOnClick = true;
            tsbFFT.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsbFFT.ImageTransparentColor = Color.Magenta;
            tsbFFT.Name = "tsbFFT";
            tsbFFT.Size = new Size(23, 22);
            tsbFFT.Text = "FFT";
            tsbFFT.Click += tsbFFT_Click;
            // 
            // frmVisWave
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(261, 251);
            Controls.Add(toolStripContainer1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            Name = "frmVisWave";
            Opacity = 0.9D;
            Text = "Visualizer";
            FormClosed += frmVisWave_FormClosed;
            Load += frmVisWave_Load;
            Shown += frmVisWave_Shown;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            toolStripContainer1.ContentPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.PerformLayout();
            toolStripContainer1.ResumeLayout(false);
            toolStripContainer1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ToolStripContainer toolStripContainer1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tsbHeight1;
        private System.Windows.Forms.ToolStripButton tsbHeight2;
        private System.Windows.Forms.ToolStripButton tsbHeight3;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton tsbDispType1;
        private System.Windows.Forms.ToolStripButton tsbDispType2;
        private System.Windows.Forms.ToolStripButton tsbFFT;
    }
}