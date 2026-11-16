using System;
using System.IO;
using System.Media;
using System.Threading;

namespace DynamicIsland.Services
{
    public interface ISoundEffectService
    {
        bool IsSoundEnabled { get; set; }
        void PlayExpand();
        void PlayCollapse();
        void PlayTap();
        void PlayChime();
        void PlayDrop();
        void PlayPop();
        void PlaySnap();
    }

    /// <summary>
    /// Процедурный синтезатор органичных звуков UI в стиле iOS / visionOS / macOS.
    /// Синтезирует синусоидальные звуковые волны в памяти без внешних файлов с микро-атакой и затуханием,
    /// полностью исключающими цифровые щелчки (clicks) и артефакты постоянной составляющей (DC offset).
    /// </summary>
    public class SoundEffectService : ISoundEffectService
    {
        public bool IsSoundEnabled { get; set; } = true;

        private readonly byte[] _expandWav;
        private readonly byte[] _collapseWav;
        private readonly byte[] _tapWav;
        private readonly byte[] _chimeWav;
        private readonly byte[] _dropWav;

        public SoundEffectService()
        {
            // Предварительная процедурная генерация WAV-семплов в памяти для моментального отклика
            // Раскрытие острова: мягкий восходящий «пузырёк» (320 Гц → 680 Гц за 55 мс, ~25% громкость)
            _expandWav = SynthesizeSweepWav(frequencyStart: 320f, frequencyEnd: 680f, durationMs: 55, volume: 0.25f, decayRate: 2.8, attackMs: 4);

            // Сворачивание острова: мягкий нисходящий спад (580 Гц → 260 Гц за 50 мс, ~22% громкость)
            _collapseWav = SynthesizeSweepWav(frequencyStart: 580f, frequencyEnd: 260f, durationMs: 50, volume: 0.22f, decayRate: 3.5, attackMs: 4);

            // Нажатие на кнопки/табы: бархатный ультракороткий «тап» виброотклика Taptic Engine (210 Гц → 140 Гц за 28 мс, ~30% громкость)
            _tapWav = SynthesizeSweepWav(frequencyStart: 210f, frequencyEnd: 140f, durationMs: 28, volume: 0.30f, decayRate: 5.2, attackMs: 3);

            // Уведомления и таймер: теплый гармоничный двойной перезвон (ноты E5 + A5 с обертонами за 380 мс)
            _chimeWav = SynthesizeChimeWav(durationMs: 380, volume: 0.26f);

            // Карман / сброс файлов: мягкая капля (380 Гц → 740 Гц за 46 мс)
            _dropWav = SynthesizeSweepWav(frequencyStart: 380f, frequencyEnd: 740f, durationMs: 46, volume: 0.24f, decayRate: 3.2, attackMs: 4);
        }

        public void PlayExpand() => PlayAsync(_expandWav);

        public void PlayCollapse() => PlayAsync(_collapseWav);

        public void PlayTap() => PlayAsync(_tapWav);

        public void PlayChime() => PlayAsync(_chimeWav);

        public void PlayDrop() => PlayAsync(_dropWav);

        // Псевдонимы обратной совместимости
        public void PlayPop() => PlayTap();

        public void PlaySnap() => PlayTap();

        /// <summary>
        /// Асинхронное воспроизведение из пула потоков без блокировки UI-потока.
        /// </summary>
        private void PlayAsync(byte[] wavBytes)
        {
            if (!IsSoundEnabled || wavBytes == null || wavBytes.Length == 0) return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using var ms = new MemoryStream(wavBytes, writable: false);
                    using var player = new SoundPlayer(ms);
                    player.PlaySync();
                }
                catch
                {
                    // Игнорируем ошибки отсутствия аудиоустройства в ОС
                }
            });
        }

        /// <summary>
        /// Процедурный синтез частотного свипа (continuous-phase sine sweep) с мягкой атакой и экспоненциальным спадом.
        /// </summary>
        private static byte[] SynthesizeSweepWav(float frequencyStart, float frequencyEnd, int durationMs, float volume, double decayRate = 3.5, int attackMs = 4)
        {
            const int sampleRate = 44100;
            int samples = Math.Max(10, (sampleRate * durationMs) / 1000);
            short[] pcm = new short[samples];

            int attackSamples = Math.Max(1, (sampleRate * attackMs) / 1000);
            int releaseSamples = Math.Max(1, (sampleRate * 3) / 1000); // 3 мс сглаженный спад в 0 для исключения щелчка

            double phase = 0.0;
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / samples;
                double freq = frequencyStart + (frequencyEnd - frequencyStart) * t;
                phase += 2.0 * Math.PI * freq / sampleRate;

                // Базовый синусоидальный генератор
                double sample = Math.Sin(phase);

                // Экспоненциальная огибающая
                double env = Math.Exp(-decayRate * t);

                // Мягкая четверть-синусоидальная атака
                if (i < attackSamples)
                {
                    double aT = (double)i / attackSamples;
                    env *= Math.Sin(Math.PI * 0.5 * aT);
                }

                // Микро-релиз в самом конце трека
                if (i >= samples - releaseSamples)
                {
                    double rT = (double)(samples - 1 - i) / releaseSamples;
                    env *= Math.Sin(Math.PI * 0.5 * rT);
                }

                int val = (int)(sample * env * volume * 32767.0);
                pcm[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return PackPcmToWav(pcm, sampleRate);
        }

        /// <summary>
        /// Процедурный синтез теплого аккорда-перезвона (нота E5 659.25 Гц + нота A5 880 Гц со сдвигом 65 мс).
        /// </summary>
        private static byte[] SynthesizeChimeWav(int durationMs = 380, float volume = 0.26f)
        {
            const int sampleRate = 44100;
            int samples = Math.Max(10, (sampleRate * durationMs) / 1000);
            short[] pcm = new short[samples];

            int note2StartSample = (sampleRate * 65) / 1000;
            int attackSamples = (sampleRate * 4) / 1000;
            int releaseSamples = (sampleRate * 5) / 1000;

            double phase1 = 0.0;
            double phase1Sub = 0.0;
            double phase2 = 0.0;
            double phase2Sub = 0.0;

            const double freq1 = 659.25; // E5
            const double freq2 = 880.00; // A5

            for (int i = 0; i < samples; i++)
            {
                double totalSample = 0.0;

                // Нота 1 (E5)
                phase1 += 2.0 * Math.PI * freq1 / sampleRate;
                phase1Sub += 2.0 * Math.PI * (freq1 * 2.0) / sampleRate; // легкий обертон
                double t1 = (double)i / samples;
                double env1 = Math.Exp(-3.2 * t1);
                if (i < attackSamples)
                {
                    env1 *= Math.Sin(Math.PI * 0.5 * ((double)i / attackSamples));
                }
                totalSample += (Math.Sin(phase1) + 0.12 * Math.Sin(phase1Sub)) * env1 * 0.65;

                // Нота 2 (A5 со смещением 65 мс)
                if (i >= note2StartSample)
                {
                    int i2 = i - note2StartSample;
                    int samples2 = samples - note2StartSample;
                    double t2 = (double)i2 / samples2;
                    phase2 += 2.0 * Math.PI * freq2 / sampleRate;
                    phase2Sub += 2.0 * Math.PI * (freq2 * 2.0) / sampleRate;
                    double env2 = Math.Exp(-3.0 * t2);
                    if (i2 < attackSamples)
                    {
                        env2 *= Math.Sin(Math.PI * 0.5 * ((double)i2 / attackSamples));
                    }
                    totalSample += (Math.Sin(phase2) + 0.10 * Math.Sin(phase2Sub)) * env2 * 0.70;
                }

                // Общий срез в конце
                if (i >= samples - releaseSamples)
                {
                    double rT = (double)(samples - 1 - i) / releaseSamples;
                    totalSample *= Math.Sin(Math.PI * 0.5 * rT);
                }

                int val = (int)(totalSample * volume * 32767.0);
                pcm[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return PackPcmToWav(pcm, sampleRate);
        }

        private static byte[] PackPcmToWav(short[] pcm, int sampleRate)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            int subChunk2Size = pcm.Length * 2;
            int chunkSize = 36 + subChunk2Size;

            bw.Write(new char[4] { 'R', 'I', 'F', 'F' });
            bw.Write(chunkSize);
            bw.Write(new char[4] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[4] { 'f', 'm', 't', ' ' });
            bw.Write(16); // PCM
            bw.Write((short)1); // PCM format
            bw.Write((short)1); // Mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2); // Byte rate
            bw.Write((short)2); // Block align
            bw.Write((short)16); // Bits per sample
            bw.Write(new char[4] { 'd', 'a', 't', 'a' });
            bw.Write(subChunk2Size);

            for (int i = 0; i < pcm.Length; i++)
            {
                bw.Write(pcm[i]);
            }

            return ms.ToArray();
        }
    }
}
