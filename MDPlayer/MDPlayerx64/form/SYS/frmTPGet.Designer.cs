namespace MDPlayer.form
{
    partial class frmTPGet
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
            groupBox1 = new GroupBox();
            btCh6 = new Button();
            btCh3 = new Button();
            btCh5 = new Button();
            btCh2 = new Button();
            btCh4 = new Button();
            btCh1 = new Button();
            dgvTonePallet = new DataGridView();
            clmNo = new DataGridViewTextBoxColumn();
            clmName = new DataGridViewTextBoxColumn();
            clmSpacer = new DataGridViewTextBoxColumn();
            label1 = new Label();
            btnCancel = new Button();
            btOK = new Button();
            btApply = new Button();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvTonePallet).BeginInit();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            groupBox1.Controls.Add(btCh6);
            groupBox1.Controls.Add(btCh3);
            groupBox1.Controls.Add(btCh5);
            groupBox1.Controls.Add(btCh2);
            groupBox1.Controls.Add(btCh4);
            groupBox1.Controls.Add(btCh1);
            groupBox1.Location = new Point(14, 151);
            groupBox1.Margin = new Padding(4, 4, 4, 4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4, 4, 4, 4);
            groupBox1.Size = new Size(308, 101);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "YM2612(To)";
            // 
            // btCh6
            // 
            btCh6.Location = new Point(208, 60);
            btCh6.Margin = new Padding(4, 4, 4, 4);
            btCh6.Name = "btCh6";
            btCh6.Size = new Size(93, 30);
            btCh6.TabIndex = 1;
            btCh6.Tag = "6";
            btCh6.Text = "Ch.6";
            btCh6.UseVisualStyleBackColor = true;
            btCh6.Click += btChn_Click;
            // 
            // btCh3
            // 
            btCh3.Location = new Point(208, 22);
            btCh3.Margin = new Padding(4, 4, 4, 4);
            btCh3.Name = "btCh3";
            btCh3.Size = new Size(93, 30);
            btCh3.TabIndex = 1;
            btCh3.Tag = "3";
            btCh3.Text = "Ch.3";
            btCh3.UseVisualStyleBackColor = true;
            btCh3.Click += btChn_Click;
            // 
            // btCh5
            // 
            btCh5.Location = new Point(107, 60);
            btCh5.Margin = new Padding(4, 4, 4, 4);
            btCh5.Name = "btCh5";
            btCh5.Size = new Size(93, 30);
            btCh5.TabIndex = 1;
            btCh5.Tag = "5";
            btCh5.Text = "Ch.5";
            btCh5.UseVisualStyleBackColor = true;
            btCh5.Click += btChn_Click;
            // 
            // btCh2
            // 
            btCh2.Location = new Point(107, 22);
            btCh2.Margin = new Padding(4, 4, 4, 4);
            btCh2.Name = "btCh2";
            btCh2.Size = new Size(93, 30);
            btCh2.TabIndex = 1;
            btCh2.Tag = "2";
            btCh2.Text = "Ch.2";
            btCh2.UseVisualStyleBackColor = true;
            btCh2.Click += btChn_Click;
            // 
            // btCh4
            // 
            btCh4.Location = new Point(7, 60);
            btCh4.Margin = new Padding(4, 4, 4, 4);
            btCh4.Name = "btCh4";
            btCh4.Size = new Size(93, 30);
            btCh4.TabIndex = 1;
            btCh4.Tag = "4";
            btCh4.Text = "Ch.4";
            btCh4.UseVisualStyleBackColor = true;
            btCh4.Click += btChn_Click;
            // 
            // btCh1
            // 
            btCh1.Location = new Point(7, 22);
            btCh1.Margin = new Padding(4, 4, 4, 4);
            btCh1.Name = "btCh1";
            btCh1.Size = new Size(93, 30);
            btCh1.TabIndex = 1;
            btCh1.Tag = "1";
            btCh1.Text = "Ch.1";
            btCh1.UseVisualStyleBackColor = true;
            btCh1.Click += btChn_Click;
            // 
            // dgvTonePallet
            // 
            dgvTonePallet.AllowUserToAddRows = false;
            dgvTonePallet.AllowUserToDeleteRows = false;
            dgvTonePallet.AllowUserToOrderColumns = true;
            dgvTonePallet.AllowUserToResizeRows = false;
            dgvTonePallet.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvTonePallet.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvTonePallet.Columns.AddRange(new DataGridViewColumn[] { clmNo, clmName, clmSpacer });
            dgvTonePallet.Location = new Point(14, 30);
            dgvTonePallet.Margin = new Padding(4, 4, 4, 4);
            dgvTonePallet.Name = "dgvTonePallet";
            dgvTonePallet.ReadOnly = true;
            dgvTonePallet.RowHeadersVisible = false;
            dgvTonePallet.RowTemplate.Height = 21;
            dgvTonePallet.Size = new Size(308, 101);
            dgvTonePallet.TabIndex = 2;
            // 
            // clmNo
            // 
            clmNo.Frozen = true;
            clmNo.HeaderText = "No.";
            clmNo.Name = "clmNo";
            clmNo.ReadOnly = true;
            clmNo.Resizable = DataGridViewTriState.False;
            clmNo.Width = 60;
            // 
            // clmName
            // 
            clmName.Frozen = true;
            clmName.HeaderText = "Name";
            clmName.Name = "clmName";
            clmName.ReadOnly = true;
            clmName.Width = 150;
            // 
            // clmSpacer
            // 
            clmSpacer.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            clmSpacer.HeaderText = "";
            clmSpacer.Name = "clmSpacer";
            clmSpacer.ReadOnly = true;
            clmSpacer.Resizable = DataGridViewTriState.False;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(19, 11);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(98, 15);
            label1.TabIndex = 1;
            label1.Text = "Tone Pallet(From)";
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(140, 260);
            btnCancel.Margin = new Padding(4, 4, 4, 4);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(88, 29);
            btnCancel.TabIndex = 0;
            btnCancel.Text = "キャンセル";
            btnCancel.UseVisualStyleBackColor = true;
            // 
            // btOK
            // 
            btOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btOK.Location = new Point(46, 260);
            btOK.Margin = new Padding(4, 4, 4, 4);
            btOK.Name = "btOK";
            btOK.Size = new Size(88, 29);
            btOK.TabIndex = 4;
            btOK.Text = "OK";
            btOK.UseVisualStyleBackColor = true;
            btOK.Click += btOK_Click;
            // 
            // btApply
            // 
            btApply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btApply.Enabled = false;
            btApply.Location = new Point(234, 260);
            btApply.Margin = new Padding(4, 4, 4, 4);
            btApply.Name = "btApply";
            btApply.Size = new Size(88, 29);
            btApply.TabIndex = 4;
            btApply.Text = "適用";
            btApply.UseVisualStyleBackColor = true;
            btApply.Click += btApply_Click;
            // 
            // frmTPGet
            // 
            AutoScaleMode = AutoScaleMode.None;
            CancelButton = btnCancel;
            ClientSize = new Size(336, 304);
            Controls.Add(btApply);
            Controls.Add(btOK);
            Controls.Add(btnCancel);
            Controls.Add(label1);
            Controls.Add(dgvTonePallet);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Margin = new Padding(4, 4, 4, 4);
            MinimizeBox = false;
            MinimumSize = new Size(352, 343);
            Name = "frmTPGet";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Get from Tone Pallet";
            Load += frmTPGet_Load;
            groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvTonePallet).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.DataGridView dgvTonePallet;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btOK;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmNo;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmName;
        private System.Windows.Forms.DataGridViewTextBoxColumn clmSpacer;
        private System.Windows.Forms.Button btCh6;
        private System.Windows.Forms.Button btCh3;
        private System.Windows.Forms.Button btCh5;
        private System.Windows.Forms.Button btCh2;
        private System.Windows.Forms.Button btCh4;
        private System.Windows.Forms.Button btCh1;
        private System.Windows.Forms.Button btApply;
    }
}