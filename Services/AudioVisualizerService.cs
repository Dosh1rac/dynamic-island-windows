using System;
using System.Windows.Threading;
using NAudio.Wave;
using DynamicIsland.Helpers;

namespace DynamicIsland.Services
{
    public class AudioVisualizerService : IAudioVisualizerService
    {
        private const int FftSize = 512;
        private const int BandCount = 14;

        private WasapiLoopbackCapture? _capture;
        private readonly DispatcherTimer _renderTimer;

        private readonly float[] _sampleBuffer = new float[FftSize];
        private int _sampleCount = 0;
        private readonly object _lock = new();

        private readonly float[] _fftReal = new float[FftSize];
        private readonly float[] _fftImag = new float[FftSize];
        private readonly float[] _smoothedBands = new float[BandCount];

        public event EventHandler<float[]>? SpectrumUpdated;
        public float[] CurrentBands => _smoothedBands;
        public bool IsRunning { get; private set; }

        public AudioVisualizerService()
        {
            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(25) // 40 FPS для эквалайзера
            };
            _renderTimer.Tick += OnRenderTick;
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                _renderTimer.Start();
                IsRunning = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioVisualizer] Не удалось инициализировать захват: {ex.Message}");
                // Запускаем таймер с легкой рандомизацией / затуханием, чтобы UI не падал
                _renderTimer.Start();
                IsRunning = false;
            }
        }

        public void Stop()
        {
            if (!IsRunning && !_renderTimer.IsEnabled) return;

            try
            {
                _renderTimer.Stop();
                if (_capture != null)
                {
                    _capture.StopRecording();
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    _capture.Dispose();
                    _capture = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioVisualizer] Ошибка остановки захвата: {ex.Message}");
            }
            finally
            {
                IsRunning = false;
                Array.Clear(_smoothedBands, 0, _smoothedBands.Length);
                SpectrumUpdated?.Invoke(this, _smoothedBands);
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || _capture == null) return;

            var waveFormat = _capture.WaveFormat;
            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int channels = waveFormat.Channels;

            lock (_lock)
            {
                for (int i = 0; i < e.BytesRecorded; i += bytesPerSample * channels)
                {
                    float sample = 0f;
                    if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        sample = BitConverter.ToSingle(e.Buffer, i);
                    }
                    else if (waveFormat.BitsPerSample == 16)
                    {
                        short val = BitConverter.ToInt16(e.Buffer, i);
                        sample = val / 32768.0f;
                    }

                    if (_sampleCount < FftSize)
                    {
                        _sampleBuffer[_sampleCount++] = sample;
                    }
                    else
                    {
                        // Сдвигаем на 1 сэмпл (кольцевой буфер)
                        Array.Copy(_sampleBuffer, 1, _sampleBuffer, 0, FftSize - 1);
                        _sampleBuffer[FftSize - 1] = sample;
                    }
                }
            }
        }

        private void OnRenderTick(object? sender, EventArgs e)
        {
            float[] targetBands;

            lock (_lock)
            {
                if (_sampleCount >= FftSize / 2)
                {
                    Array.Copy(_sampleBuffer, _fftReal, FftSize);
                    Array.Clear(_fftImag, 0, FftSize);

                    FftCalculator.ApplyHannWindow(_fftReal);
                    FftCalculator.CalculateFft(_fftReal, _fftImag);
                    targetBands = FftCalculator.ExtractBands(_fftReal, _fftImag, BandCount);
                }
                else
                {
                    targetBands = new float[BandCount];
                }
            }

            // Плавное сглаживание: быстрый подъем (attack), плавный спад (decay)
            const float attack = 0.55f;
            const float decay = 0.20f;

            for (int i = 0; i < BandCount; i++)
            {
                float target = targetBands[i];
                if (target > _smoothedBands[i])
                {
                    _smoothedBands[i] += (target - _smoothedBands[i]) * attack;
                }
                else
                {
                    _smoothedBands[i] -= (_smoothedBands[i] - target) * decay;
                }

                _smoothedBands[i] = Math.Clamp(_smoothedBands[i], 0.05f, 1.0f);
            }

            SpectrumUpdated?.Invoke(this, (float[])_smoothedBands.Clone());
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            IsRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
