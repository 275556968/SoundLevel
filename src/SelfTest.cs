using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Threading;

namespace SoundLevel
{
    /// <summary>
    /// 无界面自测：不开窗口，直接跑控制环，用来验证 COM 层和增益是否生效。
    /// 用法: selftest.exe [--seconds 6] [--tone]
    /// </summary>
    internal static class SelfTest
    {
        private static int Main(string[] args)
        {
            int seconds = 6;
            bool withTone = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seconds" && i + 1 < args.Length)
                {
                    int s;
                    if (int.TryParse(args[++i], out s)) seconds = s;
                }
                else if (args[i] == "--tone")
                {
                    withTone = true;
                }
                else if (args[i] == "--autostart-on")
                {
                    Autostart.Enable();
                    Console.WriteLine("autostart=" + Autostart.IsEnabled());
                    Console.WriteLine("command=" + Autostart.CommandLine());
                    return 0;
                }
                else if (args[i] == "--autostart-off")
                {
                    Autostart.Disable();
                    Console.WriteLine("autostart=" + Autostart.IsEnabled());
                    return 0;
                }
            }

            EngineConfig cfg = new EngineConfig();
            AudioEngine engine = new AudioEngine(cfg);

            Console.WriteLine("设备: " + engine.DeviceName);
            Console.WriteLine("目标: " + cfg.TargetDb.ToString("0.0") + " dBFS");

            SoundPlayer player = null;
            if (withTone)
            {
                SpeechClip.PrepareAsync();
                for (int i = 0; i < 60 && !SpeechClip.Ready; i++) Thread.Sleep(100);
                if (!SpeechClip.Ready)
                {
                    Console.WriteLine("语音准备失败: " + SpeechClip.Error);
                    return 4;
                }
                Console.WriteLine("测试音: " + SpeechClip.FilePath);
                player = new SoundPlayer(SpeechClip.FilePath);
                player.PlayLooping();
                Thread.Sleep(700);
                bool ok = engine.SetOwnSessionVolume(0.5f);
                Console.WriteLine("绑定自身会话: " + (ok ? "成功" : "失败"));
            }

            Stopwatch sw = Stopwatch.StartNew();
            double lastTick = 0, lastRefresh = -10, lastPrint = -10;

            while (sw.Elapsed.TotalSeconds < seconds)
            {
                double now = sw.Elapsed.TotalSeconds;
                double dt = now - lastTick;
                lastTick = now;

                engine.Tick(dt);

                if (now - lastRefresh >= 1.0)
                {
                    engine.Refresh();
                    lastRefresh = now;
                }

                if (now - lastPrint >= 1.0)
                {
                    lastPrint = now;
                    List<SessionInfo> rows = new List<SessionInfo>(engine.Sessions);
                    rows.Sort(delegate(SessionInfo a, SessionInfo b) { return a.Pid.CompareTo(b.Pid); });
                    foreach (SessionInfo s in rows)
                    {
                        Console.WriteLine(string.Format(
                            "{0,6:0.0}s  {1,-18} {2,-5} peak={3,7} env={4,7} gain={5,7} vol={6,3:0}%{7}",
                            now, s.ProcessName, s.Active ? "play" : "idle",
                            AudioEngine.FormatDb(s.PeakDb), AudioEngine.FormatDb(s.EnvDb),
                            AudioEngine.FormatDb(s.GainDb), s.LastApplied * 100,
                            s.AtLimit ? "  <= pinned at 100%" : ""));
                    }
                    Console.WriteLine("        safety=" + AudioEngine.FormatDb(engine.SafetyTrimDb) +
                                      "dB  endpoint=" + AudioEngine.FormatDb(
                                          engine.EndpointPeak <= 0 ? -120 : 20 * Math.Log(engine.EndpointPeak)) + "dB");
                }

                Thread.Sleep(100);
            }

            if (player != null) { player.Stop(); player.Dispose(); }
            engine.RestoreAll();
            engine.Dispose();
            Console.WriteLine("done.");
            return 0;
        }
    }
}
