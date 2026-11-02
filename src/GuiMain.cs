using System;
using System.Windows.Forms;

namespace SoundLevel
{
    internal static class GuiMain
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); }
            catch { }

            bool startMinimized = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "--min", StringComparison.OrdinalIgnoreCase)) startMinimized = true;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(startMinimized));
        }
    }
}
