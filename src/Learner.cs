using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SoundLevel
{
    /// <summary>
    /// 从「用户拧系统音量」这个动作里学习每个 App 的偏好偏移。
    ///
    /// 逻辑：当某个 App 在响、用户把主音量调低 Δ dB，说明它比用户想要的高 Δ dB；
    /// 于是给这个 App 记一笔 -0.5Δ 的长期偏移。反复几次后收敛，用户就不用再拧了。
    /// 主音量本身不被程序改动 —— 它仍然是用户的总开关。
    /// </summary>
    internal class Learner
    {
        public const double MaxOffsetDb = 6.0;
        public const double Gain = 0.5;          // 每次只走一半，避免过冲
        public const double MinCorrectionDb = 1.0; // 小于 1 dB 的调整当成误触

        public bool Enabled = true;
        public readonly Dictionary<string, double> Offsets =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public int LearnCount;
        public string LastNote = "";

        public event EventHandler Changed;

        private double _lastMaster = -1;
        private double _accumDb;
        private double _settleAt = -1;
        private bool _pending;

        // ------------------------------------------------------------ 持久化

        public void Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();

                    if (k.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        Enabled = v != "0";
                        continue;
                    }
                    if (k.Equals("learned", StringComparison.OrdinalIgnoreCase))
                    {
                        int n;
                        if (int.TryParse(v, out n)) LearnCount = n;
                        continue;
                    }

                    double d;
                    if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        Offsets[k] = Clamp(d);
                }
            }
            catch { }
        }

        public void Save(string path)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("; SoundLevel 从你的音量操作里学到的偏好（可手工改，也可删掉重学）");
                sb.AppendLine("enabled=" + (Enabled ? "1" : "0"));
                sb.AppendLine("learned=" + LearnCount.ToString(CultureInfo.InvariantCulture));

                foreach (KeyValuePair<string, double> kv in Offsets)
                    sb.AppendLine(kv.Key + "=" + kv.Value.ToString("0.00", CultureInfo.InvariantCulture));

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        // -------------------------------------------------------------- 观察

        public void Tick(double master, IEnumerable<SessionInfo> sessions, double now)
        {
            if (master <= 0.0) return;

            if (_lastMaster < 0)
            {
                _lastMaster = master;
                return;
            }

            if (Math.Abs(master - _lastMaster) > 0.004)
            {
                double db = 20.0 * Math.Log10(Math.Max(0.001, master) / Math.Max(0.001, _lastMaster));
                _accumDb += db;
                _lastMaster = master;
                _settleAt = now + 1.2;     // 等用户松手
                _pending = true;
                return;
            }

            if (!_pending || now < _settleAt) return;

            _pending = false;
            double total = _accumDb;
            _accumDb = 0;

            if (!Enabled) return;
            if (Math.Abs(total) < MinCorrectionDb) return;

            SessionInfo who = Dominant(sessions);
            if (who == null) return;

            double cur;
            Offsets.TryGetValue(who.ProcessName, out cur);

            double delta = Math.Max(-3.0, Math.Min(3.0, total * Gain));
            double next = Clamp(cur + delta);
            Offsets[who.ProcessName] = next;
            LearnCount++;
            LastNote = who.ProcessName + " → " + next.ToString("+0.0;-0.0") + " dB";

            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        /// <summary>只有「清楚是谁在响」时才学：一个在响，或者一个明显压过其他。</summary>
        private static SessionInfo Dominant(IEnumerable<SessionInfo> sessions)
        {
            SessionInfo best = null;
            double bestDb = -200, secondDb = -200;

            foreach (SessionInfo s in sessions)
            {
                if (!s.Active) continue;
                if (string.IsNullOrEmpty(s.ProcessName)) continue;
                if (s.ProcessName.StartsWith("pid:", StringComparison.OrdinalIgnoreCase)) continue;

                if (s.EnvDb > bestDb)
                {
                    secondDb = bestDb;
                    bestDb = s.EnvDb;
                    best = s;
                }
                else if (s.EnvDb > secondDb)
                {
                    secondDb = s.EnvDb;
                }
            }

            if (best == null) return null;
            if (bestDb - secondDb >= 6.0) return best;   // 明显是一家独大
            return null;                                 // 混在一起，分不清，别瞎学
        }

        public double OffsetFor(string process)
        {
            double v;
            return Offsets.TryGetValue(process, out v) ? v : 0.0;
        }

        public void Reset()
        {
            Offsets.Clear();
            LearnCount = 0;
            LastNote = "";
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private static double Clamp(double v)
        {
            if (v > MaxOffsetDb) return MaxOffsetDb;
            if (v < -MaxOffsetDb) return -MaxOffsetDb;
            return v;
        }
    }
}
