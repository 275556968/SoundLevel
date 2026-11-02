using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SoundLevel
{
    /// <summary>登录自启：只写当前用户的 Run 项，不需要管理员权限。</summary>
    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SoundLevel";

        public static string CommandLine()
        {
            string exe = Application.ExecutablePath;
            return "\"" + exe + "\" --min";
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    string v = k.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(v);
                }
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k != null) k.SetValue(ValueName, CommandLine(), RegistryValueKind.String);
                }
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k != null) k.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }

        /// <summary>程序被移动过位置时，把旧的启动项修正成当前路径。</summary>
        public static void Repair()
        {
            try
            {
                if (!IsEnabled()) return;

                string want = CommandLine();
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return;
                    string have = k.GetValue(ValueName) as string;
                    if (string.Equals(have, want, StringComparison.OrdinalIgnoreCase)) return;
                }

                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k != null) k.SetValue(ValueName, want, RegistryValueKind.String);
                }
            }
            catch { }
        }
    }
}
