using System;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;

namespace SoundLevel
{
    /// <summary>
    /// 校准用的测试音频：优先用程序目录下的 speech.wav，
    /// 没有就用系统 TTS 合成一段中文朗读，再统一归一化到 -12 dBFS。
    /// </summary>
    internal static class SpeechClip
    {
        public const double PeakDb = -12.0;

        private static readonly object _lock = new object();
        private static bool _ready;
        private static string _error = "";
        private static string _path = "";

        public static bool Ready { get { lock (_lock) { return _ready; } } }
        public static string Error { get { lock (_lock) { return _error; } } }

        public static string FilePath
        {
            get { lock (_lock) { return _path; } }
        }

        public static void PrepareAsync()
        {
            Thread t = new Thread(delegate()
            {
                try { EnsureFile(); }
                catch (Exception ex)
                {
                    lock (_lock) { _error = ex.Message; }
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public static void EnsureFile()
        {
            lock (_lock)
            {
                if (_ready) return;

                string custom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "speech.wav");
                string source = File.Exists(custom) ? custom : Synthesize();

                string dst = Path.Combine(Path.GetTempPath(), "soundlevel-speech.wav");
                Normalize(source, dst, PeakDb);

                _path = dst;
                _ready = true;
            }
        }

        // ------------------------------------------------------------------ tts

        private static string Synthesize()
        {
            string path = Path.Combine(Path.GetTempPath(), "soundlevel-tts.wav");
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 20000) return path;
            }
            catch { }

            string text = "这是一段测试语音，用来校准音量。" +
                          "请一边听，一边把系统音量调到你平时最舒服的位置。" +
                          "调好之后，点下面的按钮。";

            using (SpeechSynthesizer synth = new SpeechSynthesizer())
            {
                VoiceInfo pick = null;

                foreach (InstalledVoice v in synth.GetInstalledVoices())
                {
                    if (!v.Enabled) continue;
                    if (v.VoiceInfo.Culture != null &&
                        v.VoiceInfo.Culture.TwoLetterISOLanguageName == "zh")
                    {
                        pick = v.VoiceInfo;
                        break;
                    }
                    if (pick == null) pick = v.VoiceInfo;
                }

                if (pick == null) throw new InvalidOperationException("系统里没有可用的语音合成引擎，可以把 speech.wav 放到程序目录。");

                synth.SelectVoice(pick.Name);
                synth.Rate = -1;
                synth.Volume = 100;
                synth.SetOutputToWaveFile(path,
                    new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                synth.Speak(text);
            }

            return path;
        }

        // ------------------------------------------------------------- wave io

        private static void Normalize(string src, string dst, double peakDb)
        {
            byte[] data = File.ReadAllBytes(src);

            if (!(data.Length > 44 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'))
            {
                File.Copy(src, dst, true);
                return;
            }

            int channels = 1, bits = 16, rate = 44100;
            int dataOffset = -1, dataLen = 0;
            int pos = 12;

            while (pos + 8 <= data.Length)
            {
                string id = new string(new char[] { (char)data[pos], (char)data[pos + 1], (char)data[pos + 2], (char)data[pos + 3] });
                int size = BitConverter.ToInt32(data, pos + 4);
                if (size < 0) break;

                if (id == "fmt " && pos + 24 <= data.Length)
                {
                    channels = BitConverter.ToInt16(data, pos + 10);
                    rate = BitConverter.ToInt32(data, pos + 12);
                    bits = BitConverter.ToInt16(data, pos + 22);
                }
                else if (id == "data")
                {
                    dataOffset = pos + 8;
                    dataLen = Math.Min(size, data.Length - dataOffset);
                    break;
                }
                pos += 8 + size + (size & 1);
            }

            if (dataOffset < 0 || bits != 16 || channels < 1)
            {
                File.Copy(src, dst, true);
                return;
            }

            int frames = dataLen / (2 * channels);

            // 找峰值
            int peak = 0;
            for (int i = 0; i < frames * channels; i++)
            {
                short s = BitConverter.ToInt16(data, dataOffset + i * 2);
                int a = s < 0 ? -s : s;
                if (a > peak) peak = a;
            }
            if (peak < 50) peak = 50;

            double target = Math.Pow(10.0, peakDb / 20.0) * 32767.0;
            double gain = target / peak;

            int silenceFrames = (int)(rate * 0.35);

            using (FileStream fs = new FileStream(dst, FileMode.Create, FileAccess.Write))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                int outFrames = frames + silenceFrames;
                int outBytes = outFrames * channels * 2;

                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + outBytes);
                w.Write(new char[] { 'W', 'A', 'V', 'E' });
                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(rate);
                w.Write(rate * channels * 2);
                w.Write((short)(channels * 2));
                w.Write((short)16);
                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(outBytes);

                for (int i = 0; i < frames * channels; i++)
                {
                    short s = BitConverter.ToInt16(data, dataOffset + i * 2);
                    double v = s * gain;
                    if (v > 32767) v = 32767;
                    if (v < -32768) v = -32768;
                    w.Write((short)Math.Round(v));
                }

                for (int i = 0; i < silenceFrames; i++)
                    for (int c = 0; c < channels; c++)
                        w.Write((short)0);
            }
        }
    }
}
