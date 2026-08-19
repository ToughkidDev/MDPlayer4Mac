using Microsoft.Win32;
using System.Windows.Forms;

namespace FileAssociationTool
{
    public partial class FileAssociationTool : Form
    {
        public FileAssociationTool()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                string extss = txtExt.Text;
                string newPrefix = txtNewPrefix.Text;
                string iconPath = txtIconPath.Text + " , -0";
                string execPath = txtExecPath.Text;
                string subkey;
                RegistryKey key;

                string[] exts = extss.Split(";", StringSplitOptions.RemoveEmptyEntries);

                foreach (string ext in exts)
                {
                    iconPath = txtIconPath.Text;
                    if (iconPath.IndexOf("???") >= 0)
                    {
                        iconPath = iconPath.Replace("???", ext.Replace(".", ""));
                    }

                    if (!File.Exists(iconPath))
                    {
                        MessageBox.Show(string.Format(".icoファイル({0})が見つからなかったので処理を中断します", iconPath));
                        return;
                    }

                    iconPath += " , -0";
                    //explorerのUserChoiceを消す
                    subkey = string.Format("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{0}", ext);
                    deleteKey(subkey);

                    //規定の値を新しいサブキーに変更
                    subkey = string.Format("{0}", ext);
                    key = Registry.ClassesRoot.CreateSubKey(subkey);
                    subkey = string.Format("{0}{1}", newPrefix, ext);
                    key.SetValue(null, subkey);//上位
                    Registry.ClassesRoot.Close();

                    //新しいサブキーを作成
                    key = Registry.ClassesRoot.CreateSubKey(subkey);
                    //defaultIconの設定
                    key = Registry.ClassesRoot.CreateSubKey(subkey + "\\DefaultIcon");
                    key.SetValue(null, iconPath);//上位

                    key = Registry.ClassesRoot.CreateSubKey(subkey + "\\shell\\open\\command");
                    key.SetValue(null, execPath);//上位

                    //規定の値を新しいサブキーに変更
                    subkey = string.Format("Software\\Classes\\{0}", ext);
                    key = Registry.CurrentUser.CreateSubKey(subkey);
                    subkey = string.Format("{0}{1}", newPrefix, ext);
                    key.SetValue(null, subkey);//上位
                    Registry.CurrentUser.Close();

                    //新しいサブキーを作成
                    key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + subkey);
                    //defaultIconの設定
                    key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + subkey + "\\DefaultIcon");
                    key.SetValue(null, iconPath);//上位
                    key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + subkey + "\\shell\\open\\command");
                    key.SetValue(null, execPath);//上位

                    //規定の値を新しいサブキーに変更
                    subkey = string.Format("Software\\Classes\\{0}", ext);
                    key = Registry.LocalMachine.CreateSubKey(subkey);
                    subkey = string.Format("{0}{1}", newPrefix, ext);
                    key.SetValue(null, subkey);//上位
                    Registry.LocalMachine.Close();

                    //新しいサブキーを作成
                    key = Registry.LocalMachine.CreateSubKey("Software\\Classes\\" + subkey);
                    //defaultIconの設定
                    key = Registry.LocalMachine.CreateSubKey("Software\\Classes\\" + subkey + "\\DefaultIcon");
                    key.SetValue(null, iconPath);//上位
                    key = Registry.LocalMachine.CreateSubKey("Software\\Classes\\" + subkey + "\\shell\\open\\command");
                    key.SetValue(null, execPath);//上位
                }

                MessageBox.Show("Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Message: {0} StackTrace:{1}", ex.Message, ex.StackTrace));
            }
        }

        private void deleteKey(string subkey)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(subkey);
            if (key != null)
            {
                while (key.SubKeyCount > 0)
                {
                    deleteKey(subkey + "\\" + key.GetSubKeyNames()[0]);
                }
                Registry.CurrentUser.DeleteSubKey(subkey);
                Console.WriteLine($"{subkey} が削除されました。");
            }
        }

        private void FileAssociationTools_Load(object sender, EventArgs e)
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ico");
            txtExt.Text = ".vgm";


            if (Directory.Exists(path))
            {
                DirectoryInfo di = new DirectoryInfo(path);
                FileInfo[] fiAlls = di.GetFiles("*.ico");
                string pp = "";
                foreach (FileInfo f in fiAlls)
                {
                    string ext = Path.GetFileNameWithoutExtension(f.FullName);
                    if (ext.IndexOf("_") >= 0)
                    {
                        ext = ext.Substring(ext.IndexOf("_") + 1);
                    }
                    pp += string.Format(".{0};", ext);
                }
                if (!string.IsNullOrEmpty(pp))
                {
                    txtExt.Text = pp;
                    txtIconPath.Text = Path.Combine(path, "mdp_???.ico");
                }
            }
        }

        private void FileAssociationTools_Shown(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show(
@"このツールを使用するとExplorerなどの関連するレジストリを削除、更新して、ファイルのアイコンと起動アプリの指定を行います。
想定外の動作結果になることも予想されますので実行前にバックアップなどの対策を行ってください。
実行は「自己責任」になり、これによって何が起こっても当方では一切責任を持ちません。
This tool will delete and update the relevant registry of Explorer and other applications, and specify file icons and startup applications.
Please back up your computer before executing this tool, as it may cause unexpected results.
Please note that you do so at your own risk, and we will not be held responsible for any problems that may occur.",
                "警告",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (res == DialogResult.Cancel) this.Close();
        }
    }
}
