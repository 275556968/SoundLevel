using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SoundLevel
{
    internal class SessionInfo
    {
        public string Key;
        public uint Pid;
        public string ProcessName;
        public bool IsSystemSounds;
        public IAudioSessionControl2 Control;
        public ISimpleAudioVolume Volume;
        public IAudioMeterInformation Meter;

        public float Baseline;        // the user's own mixer setting, captured on first sight
        public float LastApplied;
        public double EnvDb = -120.0;
        public double PeakDb = -120.0;
        public double GainDb;
        public int Misses;
        public bool Active;
        public bool Muted;
        public bool AtLimit;          // wants more gain than the mixer slider can give
    }

    internal class EngineConfig
    {
        public double TargetDb = -12.0;
        public double AttackMs = 200.0;
        public double ReleaseMs = 2500.0;
        public double MaxBoostDb = 12.0;
        public double MaxCutDb = 24.0;
        public double SlewDbPerSec = 12.0;
        public double DeadbandDb = 0.4;
        public double GateDb = -55.0;
        public bool SafetyTrim = true;
        public bool DryRun = false;
        public bool IncludeSystemSounds = false;
        public HashSet<string> Ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool Absolute = false;   // true = ignore the user's mixer setting
    }

    internal class AudioEngine : IDisposable
    {
        private readonly EngineConfig _cfg;
        private IMMDeviceEnumerator _enumerator;
        private IMMDevice _device;
        private IAudioSessionManager2 _manager;
        private IAudioMeterInformation _endpointMeter;
        private IAudioEndpointVolume _endpointVolume;

        private readonly Dictionary<string, SessionInfo> _sessions =
            new Dictionary<string, SessionInfo>(StringComparer.Ordinal);

        private Guid _eventContext = Guid.Empty;
        private double _safetyTrimDb;
        private double _endpointPeak;
        private DateTime _lastSafetyRecover = DateTime.MinValue;

        public ICollection<SessionInfo> Sessions { get { return _sessions.Values; } }
        public double SafetyTrimDb { get { return _safetyTrimDb; } }
        public double EndpointPeak { get { return _endpointPeak; } }
        public string DeviceName { get; private set; }

        public bool Enabled = true;

        /// <summary>每个进程的长期偏好偏移（dB），由 Learner 维护。</summary>
        public IDictionary<string, double> Offsets;

        /// <summary>系统主音量 0..1（每次 Tick 刷新）。</summary>
        public double MasterScalar = -1.0;

        public double TargetDb { get { return _cfg.TargetDb; } }

        public void SetTarget(double db) { _cfg.TargetDb = db; }

        public void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on) RestoreAll();
        }

        /// <summary>把本进程自己的音频会话音量设为 v（校准测试音用），返回是否找到会话。</summary>
        public bool SetOwnSessionVolume(float v)
        {
            if (_manager == null && !AcquireDevice()) return false;

            IAudioSessionEnumerator e;
            if (_manager.GetSessionEnumerator(out e) < 0 || e == null) return false;

            int count;
            if (e.GetCount(out count) < 0) return false;

            uint me = (uint)Process.GetCurrentProcess().Id;
            bool ok = false;

            for (int i = 0; i < count; i++)
            {
                IAudioSessionControl c;
                if (e.GetSession(i, out c) < 0 || c == null) continue;

                IAudioSessionControl2 c2 = c as IAudioSessionControl2;
                if (c2 != null)
                {
                    uint pid;
                    c2.GetProcessId(out pid);
                    if (pid == me)
                    {
                        ISimpleAudioVolume vol = c as ISimpleAudioVolume;
                        if (vol != null)
                        {
                            Guid g = Guid.Empty;
                            if (vol.SetMasterVolume(v, ref g) == 0) ok = true;
                        }
                    }
                }
                Com.Release(c);
            }

            return ok;
        }

        public AudioEngine(EngineConfig cfg)
        {
            _cfg = cfg;
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            Refresh();
        }

        // ---------------------------------------------------------------- device

        private bool AcquireDevice()
        {
            try
            {
                IMMDevice dev;
                if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out dev) < 0 || dev == null)
                    return false;

                object mgrObj;
                Guid iidMgr = typeof(IAudioSessionManager2).GUID;
                if (dev.Activate(ref iidMgr, 0, IntPtr.Zero, out mgrObj) < 0 || mgrObj == null)
                    return false;

                object meterObj = null;
                Guid iidMeter = typeof(IAudioMeterInformation).GUID;
                dev.Activate(ref iidMeter, 0, IntPtr.Zero, out meterObj);

                object volObj = null;
                Guid iidVol = typeof(IAudioEndpointVolume).GUID;
                dev.Activate(ref iidVol, 0, IntPtr.Zero, out volObj);

                _device = dev;
                _manager = (IAudioSessionManager2)mgrObj;
                _endpointMeter = meterObj as IAudioMeterInformation;
                _endpointVolume = volObj as IAudioEndpointVolume;
                DeviceName = DescribeDevice(dev);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string DescribeDevice(IMMDevice dev)
        {
            string id = null;
            try
            {
                dev.GetId(out id);
            }
            catch { }

            // friendly name lives in the MMDevices registry, keyed by the endpoint GUID
            try
            {
                if (!string.IsNullOrEmpty(id))
                {
                    int dot = id.LastIndexOf('.');
                    if (dot > 0)
                    {
                        string guidPart = id.Substring(dot + 1);   // "{0.0.1.00000000}.{guid}" -> "{guid}"
                        string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\"
                                         + guidPart + @"\Properties";
                        using (var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(
                                   Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                        using (var k = hklm.OpenSubKey(keyPath))
                        {
                            if (k != null)
                            {
                                string name = k.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                                if (!string.IsNullOrEmpty(name)) return name;
                            }
                        }
                    }
                }
            }
            catch { }

            return string.IsNullOrEmpty(id) ? "(default render device)" : id;
        }

        // --------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (_manager == null && !AcquireDevice()) return;

            IAudioSessionEnumerator e;
            if (_manager.GetSessionEnumerator(out e) < 0 || e == null) return;

            int count;
            if (e.GetCount(out count) < 0) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < count; i++)
            {
                IAudioSessionControl ctrl;
                if (e.GetSession(i, out ctrl) < 0 || ctrl == null) continue;

                var c2 = ctrl as IAudioSessionControl2;
                if (c2 == null) { Com.Release(ctrl); continue; }

                uint pid = 0;
                c2.GetProcessId(out pid);
                bool systemSounds = c2.IsSystemSoundsSession() == 0;

                if (systemSounds && !_cfg.IncludeSystemSounds) continue;

                string instance;
                IntPtr p;
                if (c2.GetSessionInstanceIdentifier(out p) < 0 || p == IntPtr.Zero) instance = "pid" + pid + "-" + i;
                else instance = Com.TakeString(p);

                string proc = systemSounds ? "System Sounds" : ProcessNameOf(pid);
                if (_cfg.Ignore.Contains(proc) || proc == "SoundLevel") { Com.Release(ctrl); continue; }

                string key = instance;
                seen.Add(key);

                SessionInfo s;
                if (!_sessions.TryGetValue(key, out s))
                {
                    s = new SessionInfo();
                    s.Key = key;
                    s.Pid = pid;
                    s.ProcessName = proc;
                    s.IsSystemSounds = systemSounds;
                    s.Volume = ctrl as ISimpleAudioVolume;
                    s.Meter = ctrl as IAudioMeterInformation;
                    float v;
                    if (s.Volume != null && s.Volume.GetMasterVolume(out v) == 0) s.Baseline = v;
                    else s.Baseline = 1.0f;
                    if (s.Baseline <= 0.0005f) s.Baseline = 1.0f;
                    s.LastApplied = s.Baseline;
                    _sessions[key] = s;
                }

                // keep COM pointers fresh
                s.Control = c2;
                s.Volume = ctrl as ISimpleAudioVolume;
                s.Meter = ctrl as IAudioMeterInformation;
                s.Misses = 0;
            }

            // forget sessions that disappeared
            var dead = new List<string>();
            foreach (var kv in _sessions)
            {
                if (seen.Contains(kv.Key)) continue;
                kv.Value.Misses++;
                if (kv.Value.Misses >= 3) dead.Add(kv.Key);
            }
            foreach (var k in dead)
            {
                _sessions.Remove(k);
            }
        }

        private static string ProcessNameOf(uint pid)
        {
            if (pid == 0) return "System";
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                    return p.ProcessName;
            }
            catch
            {
                return "pid:" + pid;
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(double dt)
        {
            if (_manager == null) { AcquireDevice(); return; }

            // 设备电平：不管开不开总线保护都读，界面要显示
            if (_endpointMeter != null)
            {
                float ep;
                if (_endpointMeter.GetPeakValue(out ep) == 0) _endpointPeak = ep;
            }

            if (_endpointVolume != null)
            {
                float m;
                if (_endpointVolume.GetMasterVolumeLevelScalar(out m) == 0) MasterScalar = m;
            }

            if (_cfg.SafetyTrim && _endpointMeter != null)
            {
                double ep = _endpointPeak;
                if (ep > 0.90) _safetyTrimDb -= 2.0 * dt * 5;
                else if (ep < 0.45 && (DateTime.UtcNow - _lastSafetyRecover).TotalMilliseconds > 400)
                {
                    _safetyTrimDb += 0.5;
                    _lastSafetyRecover = DateTime.UtcNow;
                }
                if (_safetyTrimDb < -18.0) _safetyTrimDb = -18.0;
                if (_safetyTrimDb > 0.0) _safetyTrimDb = 0.0;
            }

            foreach (var s in _sessions.Values)
            {
                if (s.Control == null || s.Volume == null) continue;

                try
                {
                    AudioSessionState state;
                    s.Control.GetState(out state);
                    s.Active = state == AudioSessionState.Active;

                    bool muted = false;
                    s.Volume.GetMute(out muted);
                    s.Muted = muted;

                    float peak = 0f;
                    if (s.Meter != null) s.Meter.GetPeakValue(out peak);
                    s.PeakDb = ToDb(peak);

                    // envelope: fast on the way up, slow on the way down
                    double tau = (s.PeakDb > s.EnvDb) ? _cfg.AttackMs : _cfg.ReleaseMs;
                    double coef = 1.0 - Math.Exp(-1000.0 * dt / Math.Max(20.0, tau));
                    s.EnvDb += (s.PeakDb - s.EnvDb) * coef;

                    if (s.Active && !muted && s.EnvDb > _cfg.GateDb)
                    {
                        double off = 0.0;
                        if (Offsets != null) Offsets.TryGetValue(s.ProcessName, out off);

                        double desired = Clamp(_cfg.TargetDb - s.EnvDb + off, -_cfg.MaxCutDb, _cfg.MaxBoostDb);
                        if (Math.Abs(desired - s.GainDb) > _cfg.DeadbandDb)
                        {
                            double step = Clamp(desired - s.GainDb, -_cfg.SlewDbPerSec * dt, _cfg.SlewDbPerSec * dt);
                            s.GainDb += step;
                        }
                    }

                    if (Math.Abs(s.GainDb) > 0.05)
                    {
                        float current;
                        if (s.Volume.GetMasterVolume(out current) == 0 && s.LastApplied > 0.0005f)
                        {
                            // the user moved this slider by hand -> adopt it as the new baseline
                            if (Math.Abs(current - s.LastApplied) > 0.02f)
                            {
                                double ampNow = Math.Pow(10.0, (s.GainDb + _safetyTrimDb) / 20.0);
                                if (ampNow > 0.0005)
                                {
                                    s.Baseline = ClampF((float)(current / ampNow), 0.002f, 1.0f);
                                    s.LastApplied = current;
                                }
                            }
                        }
                    }

                    double totalDb = s.GainDb + _safetyTrimDb;
                    double amp = Math.Pow(10.0, totalDb / 20.0);
                    float target = _cfg.Absolute
                        ? (float)Clamp(amp, 0.002, 1.0)
                        : (float)Clamp(s.Baseline * amp, 0.002, 1.0);

                    // the mixer slider tops out at 100%: report when we are pinned there
                    s.AtLimit = s.GainDb > 0.5 && !_cfg.Absolute && (s.Baseline * amp >= 0.998);

                    if (Enabled && !_cfg.DryRun && s.Active)
                    {
                        if (Math.Abs(target - s.LastApplied) > 0.002f)
                        {
                            s.Volume.SetMasterVolume(target, ref _eventContext);
                            s.LastApplied = target;
                        }
                    }
                }
                catch
                {
                    // never let one session take the whole loop down
                }
            }
        }

        // ---------------------------------------------------------------- restore

        public void RestoreAll()
        {
            foreach (var s in _sessions.Values)
            {
                try
                {
                    if (s.Volume != null) s.Volume.SetMasterVolume(s.Baseline, ref _eventContext);
                }
                catch { }
            }
        }

        // ----------------------------------------------------------------- utils

        private static double ToDb(float linear)
        {
            if (linear <= 0.00001f) return -120.0;
            return 20.0 * Math.Log10(linear);
        }

        public static double Clamp(double v, double lo, double hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private static float ClampF(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        public static string FormatDb(double db)
        {
            if (db <= -119.0) return "  -inf";
            return db.ToString("+0.0;-0.0; 0.0"); 
        }

        public void Dispose()
        {
            Com.Release(_endpointMeter);
            Com.Release(_manager);
            Com.Release(_device);
            Com.Release(_enumerator);
        }
    }
}
