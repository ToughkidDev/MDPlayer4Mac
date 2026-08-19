namespace MDPlayer.form
{
    public partial class ucSettingInstruments : UserControl
    {
        public ucSettingInstruments()
        {
            InitializeComponent();
        }

        private void cbSendWait_CheckedChanged(object sender, EventArgs e)
        {
            cbTwice.Enabled = cbSendWait.Checked;
        }

        private void rbAY8910P_Emu2_CheckedChanged(object sender, EventArgs e)
        {
            cbAY8910P_Emu2YMmode.Enabled = rbAY8910P_Emu2.Checked;
        }

        private void rbAY8910S_Emu2_CheckedChanged(object sender, EventArgs e)
        {
            cbAY8910S_Emu2YMmode.Enabled = rbAY8910S_Emu2.Checked;
        }
    }
}
