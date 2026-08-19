using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using System.Xml.Linq;

namespace FileAssociationTool
{
    partial class FileAssociationTool
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            button1 = new Button();
            txtExt = new TextBox();
            label1 = new Label();
            txtNewPrefix = new TextBox();
            label2 = new Label();
            label3 = new Label();
            txtIconPath = new TextBox();
            label4 = new Label();
            txtExecPath = new TextBox();
            label5 = new Label();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button1.Location = new Point(444, 145);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 0;
            button1.Text = "実行";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // txtExt
            // 
            txtExt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtExt.Location = new Point(219, 6);
            txtExt.Name = "txtExt";
            txtExt.Size = new Size(300, 23);
            txtExt.TabIndex = 1;
            txtExt.Text = ".vgm";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(43, 15);
            label1.TabIndex = 2;
            label1.Text = "拡張子";
            // 
            // txtNewPrefix
            // 
            txtNewPrefix.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtNewPrefix.Location = new Point(219, 35);
            txtNewPrefix.Name = "txtNewPrefix";
            txtNewPrefix.Size = new Size(136, 23);
            txtNewPrefix.TabIndex = 1;
            txtNewPrefix.Text = "MDPlayer";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 38);
            label2.Name = "label2";
            label2.Size = new Size(144, 15);
            label2.TabIndex = 2;
            label2.Text = "新しいレジストリキーの前置詞";
            // 
            // label3
            // 
            label3.Location = new Point(12, 66);
            label3.Name = "label3";
            label3.Size = new Size(201, 34);
            label3.TabIndex = 4;
            label3.Text = ".icoファイルがあるフルパス(???があると拡張子で指定した文字列に差し替えます)";
            // 
            // txtIconPath
            // 
            txtIconPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtIconPath.Location = new Point(219, 66);
            txtIconPath.Name = "txtIconPath";
            txtIconPath.Size = new Size(300, 23);
            txtIconPath.TabIndex = 3;
            txtIconPath.Text = "c:\\ico\\mdp_???.ico";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 107);
            label4.Name = "label4";
            label4.Size = new Size(201, 15);
            label4.TabIndex = 6;
            label4.Text = "実行するプログラムのフルパス(とオプション)";
            // 
            // txtExecPath
            // 
            txtExecPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtExecPath.Location = new Point(219, 104);
            txtExecPath.Name = "txtExecPath";
            txtExecPath.Size = new Size(300, 23);
            txtExecPath.TabIndex = 5;
            txtExecPath.Text = "\"C:\\MDPlayer\\MDPlayerx64.exe\" \"%1\"";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(12, 153);
            label5.Name = "label5";
            label5.Size = new Size(392, 15);
            label5.TabIndex = 6;
            label5.Text = "一通り設定したらOS再起動またはログオフ、ログインを行うと設定が読み込まれます";
            // 
            // FileAssociationTool
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(543, 181);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(txtExecPath);
            Controls.Add(label3);
            Controls.Add(txtIconPath);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(txtNewPrefix);
            Controls.Add(txtExt);
            Controls.Add(button1);
            MaximumSize = new Size(10000, 220);
            MinimumSize = new Size(559, 220);
            Name = "FileAssociationTool";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "FileAssociationTool";
            Load += FileAssociationTools_Load;
            Shown += FileAssociationTools_Shown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private TextBox txtExt;
        private Label label1;
        private TextBox txtNewPrefix;
        private Label label2;
        private Label label3;
        private TextBox txtIconPath;
        private Label label4;
        private TextBox txtExecPath;
        private Label label5;
    }
}
