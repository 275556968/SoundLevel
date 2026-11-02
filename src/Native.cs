using System;
using System.Runtime.InteropServices;

namespace SoundLevel
{
    internal static class Native
    {
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        internal const int SW_SHOW = 5;
        internal const int SW_RESTORE = 9;

        internal const int WM_NCLBUTTONDOWN = 0xA1;
        internal const int HTCAPTION = 0x2;

        /// <summary>已经有实例在跑时，把它从托盘/最小化里叫出来。</summary>
        internal static bool ActivateExisting(string title)
        {
            IntPtr h = FindWindow(null, title);
            if (h == IntPtr.Zero) return false;

            ShowWindow(h, SW_RESTORE);
            ShowWindow(h, SW_SHOW);
            SetForegroundWindow(h);
            return true;
        }
    }
}
