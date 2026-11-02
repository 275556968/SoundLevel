using System;
using System.Threading;
using System.Windows.Forms;

namespace SoundLevel
{
    internal static class GuiMain
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool startMinimized = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "--min", StringComparison.OrdinalIgnoreCase)) startMinimized = true;
            }

            // 只允许一个实例：否则两个进程会抢同一批音频会话
            bool created;
            using (Mutex mutex = new Mutex(true, "SoundLevel.SingleInstance.v1", out created))
            {
                if (!created)
                {
                    if (!Native.ActivateExisting("SoundLevel"))
                        MessageBox.Show("SoundLevel 已经在运行（看右下角托盘图标）。", "SoundLevel");
                    return;
                }

                try { Native.SetProcessDPIAware(); }
                catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(startMinimized));
            }
        }
    }
}
