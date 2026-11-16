using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DynamicIsland.Services
{
    public interface ISoundEffectService : IDisposable
    {
        bool IsSoundEnabled { get; set; }
        float MasterVolume { get; set; }
        void PlayExpand();
        void PlayCollapse();
        void PlayTap();
        void PlayChime();
        void PlayDrop();
        void PlayPop();
        void PlaySnap();
    }

    /// <summary>
    /// Сервис звуковых эффектов на базе оригинальных тактильных аудиофайлов Apple iOS / watchOS (UI Haptics).
    /// Использует полифонический микшер NAudio (MixingSampleProvider) с предварительно кэшированными
    /// в память 16-битными PCM-семплами:
    /// - Полная поддержка наложения звуков (одновременное воспроизведение тапов, анимаций и уведомлений)
    /// - Тихая ненавязчивая громкость (0.25 для кликов и жестов, 0.40 для таймера) без резких мелодий
    /// - Мгновенный отклик 0 мс (все аудиоданные предзагружены в RAM)
    /// - Отказоустойчивый запуск без исключений при отсутствии активного аудиоустройства
    /// </summary>
    public class SoundEffectService : ISoundEffectService
    {
        public bool IsSoundEnabled { get; set; } = true;
        public float MasterVolume { get; set; } = 1.0f;

        private readonly MixingSampleProvider? _mixer;
        private readonly WaveOutEvent? _waveOut;
        private readonly bool _isAudioAvailable;

        private readonly CachedSound? _tapSound;
        private readonly CachedSound? _expandSound;
        private readonly CachedSound? _collapseSound;
        private readonly CachedSound? _chimeSound;
        private readonly CachedSound? _dropSound;

        public SoundEffectService()
        {
            // 1. Предварительная загрузка оригинальных аудиофайлов Apple в память (RAM)
            _tapSound = LoadCachedSound("tap.wav");
            _expandSound = LoadCachedSound("expand.wav");
            _collapseSound = LoadCachedSound("collapse.wav");
            _chimeSound = LoadCachedSound("chime.wav");
            _dropSound = LoadCachedSound("drop.wav");

            // 2. Инициализация микшера NAudio для наложения звуков без блокировки UI
            try
            {
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                _mixer = new MixingSampleProvider(waveFormat)
                {
                    ReadFully = true
                };

                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 50,
                    NumberOfBuffers = 3
                };
                _waveOut.Init(_mixer);
                _waveOut.Play();
                _isAudioAvailable = true;
            }
            catch (Exception ex)
            {
                _isAudioAvailable = false;
                App.Log($"[SoundEffectService] Audio hardware not available: {ex.Message}");
            }
        }

        // Раскрытие острова: мягкий тактильный микро-импульс (<120 мс, Material navigation_forward-selection)
        public void PlayExpand() => PlaySound(_expandSound, 0.18f);

        // Сворачивание острова: мягкий тактильный микро-импульс (<125 мс, Material navigation_backward-selection)
        public void PlayCollapse() => PlaySound(_collapseSound, 0.18f);

        // Клик по табам и кнопкам: еле слышный бархатный отклик (60 мс, Material ui_tap-simple)
        public void PlayTap() => PlaySound(_tapSound, 0.12f);

        // Завершение таймера / уведомление: мягкий ненавязчивый аккорд (Material notification_simple-01)
        public void PlayChime() => PlaySound(_chimeSound, 0.30f);

        // Сброс файлов в карман: мягкий короткий отклик (<90 мс)
        public void PlayDrop() => PlaySound(_dropSound, 0.18f);

        // Псевдонимы обратной совместимости
        public void PlayPop() => PlayTap();

        public void PlaySnap() => PlayTap();

        private void PlaySound(CachedSound? sound, float volumeScale)
        {
            if (!IsSoundEnabled || !_isAudioAvailable || _mixer == null || sound == null) return;

            try
            {
                var input = new CachedSoundSampleProvider(sound);
                var volumeProvider = new VolumeSampleProvider(input)
                {
                    Volume = Math.Clamp(MasterVolume * volumeScale, 0.0f, 1.0f)
                };
                _mixer.AddMixerInput(volumeProvider);
            }
            catch (Exception ex)
            {
                App.Log($"[SoundEffectService] Playback error: {ex.Message}");
            }
        }

        private static CachedSound? LoadCachedSound(string fileName)
        {
            try
            {
                byte[]? wavBytes = LoadSoundBytes(fileName);
                if (wavBytes != null && wavBytes.Length > 0)
                {
                    return new CachedSound(wavBytes);
                }
            }
            catch (Exception ex)
            {
                App.Log($"[SoundEffectService] Error caching {fileName}: {ex.Message}");
            }
            return null;
        }

        private static byte[]? LoadSoundBytes(string fileName)
        {
            try
            {
                // 1. Попытка загрузить из папки приложения (Assets/Sounds/<file>)
                string diskPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", fileName);
                if (File.Exists(diskPath))
                {
                    return File.ReadAllBytes(diskPath);
                }

                // 2. Попытка загрузить из встроенных ресурсов (EmbeddedResource)
                var asm = Assembly.GetExecutingAssembly();
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(resName))
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log($"[SoundEffectService] Error reading {fileName}: {ex.Message}");
            }
            return null;
        }

        public void Dispose()
        {
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
            }
            catch { }
        }
    }

    /// <summary>
    /// Предварительно декодированный в память аудиофрагмент.
    /// Хранит сырые 32-битные float-сэмплы для мгновенного подмешивания в микшер без дисковых операций.
    /// </summary>
    public class CachedSound
    {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }

        public CachedSound(byte[] wavBytes)
        {
            using var ms = new MemoryStream(wavBytes);
            using var reader = new WaveFileReader(ms);
            var sampleProvider = reader.ToSampleProvider();
            WaveFormat = sampleProvider.WaveFormat;

            var wholeFile = new List<float>((int)(reader.Length / 2));
            float[] buffer = new float[4096];
            int samplesRead;
            while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    wholeFile.Add(buffer[i]);
                }
            }
            AudioData = wholeFile.ToArray();
        }
    }

    /// <summary>
    /// Легковесный потокобезопасный провайдер сэмплов для воспроизведения CachedSound в MixingSampleProvider.
    /// </summary>
    public class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cachedSound;
        private long _position;

        public CachedSoundSampleProvider(CachedSound cachedSound)
        {
            _cachedSound = cachedSound;
        }

        public WaveFormat WaveFormat => _cachedSound.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            long availableSamples = _cachedSound.AudioData.Length - _position;
            long samplesToCopy = Math.Min(availableSamples, count);
            Array.Copy(_cachedSound.AudioData, _position, buffer, offset, samplesToCopy);
            _position += samplesToCopy;
            return (int)samplesToCopy;
        }
    }
}
