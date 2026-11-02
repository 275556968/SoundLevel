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

        internal const int WM_NCLBUTTONDOWN = 0xA1;
        internal const int HTCAPTION = 0x2;
    }
}
