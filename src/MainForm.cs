using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace SoundLevel
{
    internal class MainForm : Form
    {
        private readonly EngineConfig _cfg = new EngineConfig();
        private readonly AudioEngine _engine;
        private readonly Timer _timer = new Timer();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private FlatButton _applyBtn;
        private LevelSlider _slider;
        private PillToggle _toggle;
        private PillToggle _autoToggle;
        private CircleButton _playBtn;
        private IconButton _minBtn;
        private IconButton _closeBtn;

        private SoundPlayer _player;
        private bool _playing;
        private bool _toneBound;
        private int _bindTries;

        private double _refDb = -12.0;
        private bool _calibrated;
        private string _calibratedAt = "";
        private double _flashUntil = -1;

        private double _lastTick;
        private double _lastRefresh = -10;
        private readonly bool _startMinimized;

        private const int W = 440;
        private const int H = 608;

        public MainForm(bool startMinimized)
        {
            _startMinimized = startMinimized;
            LoadIni(_cfg);

            double refDb;
            string when;
            if (Calibration.TryLoad(out refDb, out when))
            {
                _refDb = refDb;
                _calibrated = true;
                _calibratedAt = when;
                _cfg.TargetDb = refDb;
            }

            _engine = new AudioEngine(_cfg);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Text = "SoundLevel";
            ShowInTaskbar = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _timer.Interval = 100;
            _timer.Tick += OnTick;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Theme.Scale = DeviceDpi / 96f;
            ClientSize = new Size(Theme.S(W), Theme.S(H));
            BuildLayout();
            SetRoundedRegion();

            SpeechClip.PrepareAsync();
            Autostart.Repair();
            _timer.Start();

            if (_startMinimized) WindowState = FormWindowState.Minimized;
        }

        // ------------------------------------------------------------- layout

        private void BuildLayout()
        {
            _minBtn = new IconButton();
            _minBtn.IsClose = false;
            _minBtn.Bounds = new Rectangle(Theme.S(W - 72), Theme.S(10), Theme.S(28), Theme.S(28));
            _minBtn.Click += delegate { WindowState = FormWindowState.Minimized; };
            Controls.Add(_minBtn);

            _closeBtn = new IconButton();
            _closeBtn.IsClose = true;
            _closeBtn.Bounds = new Rectangle(Theme.S(W - 40), Theme.S(10), Theme.S(28), Theme.S(28));
            _closeBtn.Click += delegate { Close(); };
            Controls.Add(_closeBtn);

            _playBtn = new CircleButton();
            _playBtn.Bounds = new Rectangle(Theme.S(34), Theme.S(138), Theme.S(56), Theme.S(56));
            _playBtn.Click += delegate { ToggleTone(); };
            _playBtn.BackColor = Theme.Card;
            Controls.Add(_playBtn);

            _slider = new LevelSlider();
            _slider.Bounds = new Rectangle(Theme.S(34), Theme.S(206), Theme.S(W - 68), Theme.S(26));
            _slider.BackColor = Theme.Card;
            _slider.ValueChanged += delegate
            {
                if (_playing && _toneBound) _engine.SetOwnSessionVolume((float)_slider.Value);
                Invalidate();
            };
            Controls.Add(_slider);

            double initial = 0.62;
            if (_calibrated) initial = Math.Pow(10.0, (_refDb - SpeechClip.PeakDb) / 20.0);
            _slider.SetValueSilent(initial);

            _applyBtn = new FlatButton();
            _applyBtn.Accent = true;
            _applyBtn.Text = "就用这个响度";
            _applyBtn.Bounds = new Rectangle(Theme.S(34), Theme.S(258), Theme.S(W - 68), Theme.S(40));
            _applyBtn.Click += delegate { ApplyCalibration(); };
            Controls.Add(_applyBtn);

            _toggle = new PillToggle();
            _toggle.Checked = true;
            _toggle.Bounds = new Rectangle(Theme.S(18), Theme.S(H - 44), Theme.S(104), Theme.S(30));
            _toggle.CheckedChanged += delegate
            {
                _engine.SetEnabled(_toggle.Checked);
                Invalidate();
            };
            Controls.Add(_toggle);

            _autoToggle = new PillToggle();
            _autoToggle.LabelOn = "开机自启";
            _autoToggle.LabelOff = "开机自启";
            _autoToggle.Bounds = new Rectangle(Theme.S(130), Theme.S(H - 44), Theme.S(104), Theme.S(30));
            _autoToggle.Checked = Autostart.IsEnabled();
            _autoToggle.CheckedChanged += delegate
            {
                if (_autoToggle.Checked) Autostart.Enable();
                else Autostart.Disable();
            };
            Controls.Add(_autoToggle);
        }

        private void SetRoundedRegion()
        {
            using (GraphicsPath p = Theme.RoundRect(new Rectangle(0, 0, Width, Height), Theme.S(14)))
                Region = new Region(p);
        }

        // -------------------------------------------------------------- paint

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush b = new SolidBrush(Theme.Bg))
                g.FillRectangle(b, ClientRectangle);

            DrawTitle(g);
            DrawCard(g);
            DrawSessions(g);
            DrawFooter(g);
        }

        private void DrawTitle(Graphics g)
        {
            int d = Theme.S(8);
            using (SolidBrush b = new SolidBrush(Theme.Accent))
                g.FillEllipse(b, Theme.S(24), Theme.S(19), d, d);

            Theme.DrawText(g, "SoundLevel", Theme.FApp, Theme.Text, Theme.S(40), Theme.S(13));
        }

        private void DrawCard(Graphics g)
        {
            Rectangle card = new Rectangle(Theme.S(18), Theme.S(56), Theme.S(W - 36), Theme.S(284));
            Theme.FillRound(g, card, Theme.S(14), Theme.Card);
            Theme.DrawRound(g, card, Theme.S(14), Theme.Border, 1f);

            Theme.DrawText(g, "校准你的舒适响度", Theme.FTitle, Theme.Text, Theme.S(34), Theme.S(74));
            Theme.DrawText(g, "播放测试音，拖到你听着舒服为止", Theme.FBody, Theme.Dim, Theme.S(34), Theme.S(108));

            Theme.DrawText(g, "测试音 · 人声朗读", Theme.FBody, Theme.Text, Theme.S(104), Theme.S(144));
            Theme.DrawText(g, "平常语速，接近真实听感", Theme.FSmall, Theme.Faint, Theme.S(104), Theme.S(164));

            string pct = Math.Round(_slider.Value * 100).ToString("0") + "%";
            Theme.TextRight(g, pct, Theme.FBig, Theme.Accent, Theme.S(W - 34), Theme.S(128));

            Theme.DrawText(g, "小", Theme.FSmall, Theme.Dim, Theme.S(34), Theme.S(228));
            Theme.TextRight(g, "大", Theme.FSmall, Theme.Dim, Theme.S(W - 34), Theme.S(228));

            string status;
            Color statusColor;
            if (_calibrated)
            {
                status = "已校准 · 参考 " + _refDb.ToString("0.0") + " dBFS";
                if (_calibratedAt.Length > 0) status += " · " + _calibratedAt;
                statusColor = Theme.Good;
            }
            else
            {
                status = "尚未校准 · 当前使用默认 " + _cfg.TargetDb.ToString("0.0") + " dBFS";
                statusColor = Theme.Faint;
            }
            Theme.DrawText(g, status, Theme.FSmall, statusColor, Theme.S(34), Theme.S(310));
        }

        private void DrawSessions(Graphics g)
        {
            List<SessionInfo> rows = new List<SessionInfo>(_engine.Sessions);
            rows.Sort(delegate(SessionInfo a, SessionInfo b)
            {
                if (a.Active != b.Active) return a.Active ? -1 : 1;
                return string.Compare(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase);
            });

            string header = rows.Count > 0 ? "正在发声 · " + rows.Count : "正在发声";
            Theme.DrawText(g, header, Theme.FBody, Theme.Dim, Theme.S(20), Theme.S(352));

            Theme.TextRight(g, "目标 " + _cfg.TargetDb.ToString("0.0") + " dBFS",
                Theme.FSmall, Theme.Faint, Theme.S(W - 20), Theme.S(355));

            if (rows.Count == 0)
            {
                Theme.DrawText(g, "现在没有声音在播放", Theme.FBody, Theme.Faint, Theme.S(20), Theme.S(396));
                Theme.DrawText(g, "打开点音乐或视频试试", Theme.FSmall, Theme.Faint, Theme.S(20), Theme.S(418));
                return;
            }

            int y = Theme.S(374);
            int maxRows = 4;
            for (int i = 0; i < rows.Count && i < maxRows; i++)
            {
                DrawRow(g, rows[i], y);
                y += Theme.S(44);
            }
        }

        private void DrawRow(Graphics g, SessionInfo s, int y)
        {
            bool active = s.Active && _toggle.Checked;
            Color nameColor = active ? Theme.Text : Theme.Dim;

            Theme.DrawText(g, Truncate(s.ProcessName, 14), Theme.FName, nameColor, Theme.S(20), y + Theme.S(2));

            double effDb = 0.0;
            if (s.Baseline > 0.0001f && s.LastApplied > 0.0001f)
                effDb = 20.0 * Math.Log10(s.LastApplied / s.Baseline);

            string gain = FormatGain(effDb);
            Color gainColor = effDb < -0.3 ? Theme.Accent : (effDb > 0.3 ? Theme.Warn : Theme.Faint);
            if (!active) gainColor = Theme.Faint;
            Theme.TextRight(g, gain, Theme.FSmall, gainColor, Theme.S(W - 20), y + Theme.S(3));

            int barX = Theme.S(142);
            int barW = Theme.S(158);
            int barY = y + Theme.S(10);
            int barH = Theme.S(6);

            Rectangle track = new Rectangle(barX, barY, barW, barH);
            Theme.FillRound(g, track, barH / 2, Theme.TrackBg);

            double p = (s.EnvDb + 60.0) / 60.0;
            if (p < 0) p = 0;
            if (p > 1) p = 1;
            int fillW = (int)Math.Round(barW * p);
            if (fillW > 2)
            {
                Color fillColor = !active ? Theme.Faint : (s.AtLimit ? Theme.Warn : Theme.Accent);
                Rectangle fill = new Rectangle(barX, barY, fillW, barH);
                Theme.FillRound(g, fill, barH / 2, fillColor);
            }

            string vol = (s.LastApplied * 100).ToString("0") + "%" + (s.AtLimit ? "↑" : "");
            Theme.DrawText(g, vol, Theme.FSmall, active ? Theme.Dim : Theme.Faint, Theme.S(308), y + Theme.S(3));
        }

        private void DrawFooter(Graphics g)
        {
            if (_engine.Sessions.Count <= 3)
            {
                Theme.DrawText(g, "增益为负 = 正在压低   ↑ = 音量已顶到 100%", Theme.FSmall, Theme.Faint,
                    Theme.S(20), Theme.S(H - 62));
            }

            string right;
            if (!_toggle.Checked) right = "已暂停";
            else
            {
                int n = 0;
                foreach (SessionInfo s in _engine.Sessions) if (s.Active) n++;
                right = n == 0 ? "等待声音…" : "平衡中 · " + n + " 路";
            }
            Theme.TextRight(g, right, Theme.FSmall, Theme.Faint, Theme.S(W - 18), Theme.S(H - 34));
        }

        private static string FormatGain(double db)
        {
            if (db > -0.05 && db < 0.05) return "0.0 dB";
            return db.ToString("+0.0;-0.0") + " dB";
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }

        // --------------------------------------------------------------- tone

        private void ToggleTone()
        {
            if (_playing) StopTone();
            else StartTone();
        }

        private void StartTone()
        {
            if (!SpeechClip.Ready)
            {
                MessageBox.Show(SpeechClip.Error.Length > 0
                        ? "语音准备失败：" + SpeechClip.Error
                        : "语音还在准备中，稍等一下再点。",
                    "SoundLevel");
                return;
            }

            try
            {
                _player = new SoundPlayer(SpeechClip.FilePath);
                _player.PlayLooping();
                _playing = true;
                _toneBound = false;
                _bindTries = 0;
                _playBtn.Playing = true;
                _playBtn.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("测试音播放失败：" + ex.Message, "SoundLevel");
            }
        }

        private void StopTone()
        {
            try
            {
                if (_player != null)
                {
                    _player.Stop();
                    _player.Dispose();
                    _player = null;
                }
            }
            catch { }

            _playing = false;
            _toneBound = false;
            if (_playBtn != null)
            {
                _playBtn.Playing = false;
                _playBtn.Invalidate();
            }
            try { _engine.SetOwnSessionVolume(1.0f); } catch { }
        }

        private void ApplyCalibration()
        {
            double v = Math.Max(0.02, _slider.Value);
            _refDb = SpeechClip.PeakDb + 20.0 * Math.Log10(v);
            _calibrated = true;
            _calibratedAt = DateTime.Now.ToString("MM-dd HH:mm");
            _cfg.TargetDb = _refDb;
            Calibration.Save(_refDb);

            _flashUntil = _clock.Elapsed.TotalSeconds + 1.6;
            SetApplyText("已应用");
            Invalidate();
        }

        // --------------------------------------------------------------- tick

        private void OnTick(object sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = now - _lastTick;
            _lastTick = now;

            try { _engine.Tick(dt); } catch { }

            if (now - _lastRefresh >= 1.0)
            {
                try { _engine.Refresh(); } catch { }
                _lastRefresh = now;
            }

            if (_playing && !_toneBound && _bindTries < 40)
            {
                _bindTries++;
                try { _toneBound = _engine.SetOwnSessionVolume((float)_slider.Value); }
                catch { }
            }

            if (_flashUntil > 0 && now > _flashUntil) _flashUntil = -1;

            if (!SpeechClip.Ready)
            {
                _playBtn.Enabled = false;
                SetApplyText(SpeechClip.Error.Length > 0 ? "语音准备失败" : "正在准备语音…");
            }
            else
            {
                _playBtn.Enabled = true;
                if (_flashUntil < 0) SetApplyText(_playing ? "调好后点这里" : "就用这个响度");
            }

            Invalidate();
        }

        private void SetApplyText(string t)
        {
            if (_applyBtn != null && _applyBtn.Text != t) _applyBtn.Text = t;
        }

        // -------------------------------------------------------------- misc

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < Theme.S(52))
            {
                Native.ReleaseCapture();
                Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, Native.HTCAPTION, 0);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopTone();
            try { _engine.RestoreAll(); } catch { }
            try { _engine.Dispose(); } catch { }
            base.OnFormClosing(e);
        }

        private static void LoadIni(EngineConfig cfg)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                if (!File.Exists(path)) return;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();

                    switch (k)
                    {
                        case "target": cfg.TargetDb = D(v, cfg.TargetDb); break;
                        case "attack": cfg.AttackMs = D(v, cfg.AttackMs); break;
                        case "release": cfg.ReleaseMs = D(v, cfg.ReleaseMs); break;
                        case "max_boost": cfg.MaxBoostDb = D(v, cfg.MaxBoostDb); break;
                        case "max_cut": cfg.MaxCutDb = D(v, cfg.MaxCutDb); break;
                        case "gate": cfg.GateDb = D(v, cfg.GateDb); break;
                        case "safety": cfg.SafetyTrim = v != "0" && v.ToLowerInvariant() != "false"; break;
                        case "absolute": cfg.Absolute = v == "1" || v.ToLowerInvariant() == "true"; break;
                        case "system_sounds": cfg.IncludeSystemSounds = v == "1"; break;
                        case "ignore":
                            foreach (string n in v.Split(','))
                                if (n.Trim().Length > 0) cfg.Ignore.Add(n.Trim());
                            break;
                    }
                }
            }
            catch { }
        }

        private static double D(string s, double fallback)
        {
            double v;
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
    }
}
