using System;
using System.Globalization;
using System.IO;

namespace SoundLevel
{
    internal static class Calibration
    {
        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calibration.ini"); }
        }

        public static bool TryLoad(out double refDb, out string when)
        {
            refDb = -12.0;
            when = "";
            bool found = false;

            try
            {
                if (!File.Exists(FilePath)) return false;

                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();

                    if (k == "ref_level_db")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        {
                            refDb = d;
                            found = true;
                        }
                    }
                    else if (k == "calibrated_at")
                    {
                        when = v;
                    }
                }
            }
            catch { }

            return found;
        }

        public static void Save(double refDb)
        {
            try
            {
                string when = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                string text =
                    "; SoundLevel 校准结果（由程序写入）\r\n" +
                    "ref_level_db=" + refDb.ToString("0.0", CultureInfo.InvariantCulture) + "\r\n" +
                    "calibrated_at=" + when + "\r\n";
                File.WriteAllText(FilePath, text, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
