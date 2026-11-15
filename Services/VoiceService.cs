using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DynamicIsland.Services
{
    public interface IVoiceService : IDisposable
    {
        bool IsRecording { get; }
        void StartRecording();
        Task<string> StopRecordingAndTranscribeAsync(string apiKey, string baseUrl);
    }

    public class VoiceService : IVoiceService
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioStream;
        private WaveFileWriter? _waveWriter;

        public bool IsRecording { get; private set; }

        public void StartRecording()
        {
            if (IsRecording) return;

            try
            {
                _audioStream = new MemoryStream();
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz Mono 16-bit PCM — формат для Whisper
                };

                _waveWriter = new WaveFileWriter(_audioStream, _waveIn.WaveFormat);

                _waveIn.DataAvailable += (s, e) =>
                {
                    _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
                };

                _waveIn.StartRecording();
                IsRecording = true;
            }
            catch (Exception ex)
            {
                App.Log($"VoiceService StartRecording error: {ex.Message}");
                IsRecording = false;
            }
        }

        public async Task<string> StopRecordingAndTranscribeAsync(string apiKey, string baseUrl)
        {
            if (!IsRecording || _waveIn == null || _waveWriter == null || _audioStream == null)
            {
                return string.Empty;
            }

            try
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;

                _waveWriter.Flush();
                _waveWriter.Dispose();
                _waveWriter = null;

                byte[] wavBytes = _audioStream.ToArray();
                _audioStream.Dispose();
                _audioStream = null;

                IsRecording = false;

                if (wavBytes.Length < 4000)
                {
                    return string.Empty; // Слишком короткая запись
                }

                // Отправка на Whisper API
                return await TranscribeViaWhisperApiAsync(wavBytes, apiKey, baseUrl);
            }
            catch (Exception ex)
            {
                App.Log($"VoiceService StopRecording error: {ex.Message}");
                IsRecording = false;
                return string.Empty;
            }
        }

        private async Task<string> TranscribeViaWhisperApiAsync(byte[] wavBytes, string apiKey, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Для голосового ввода укажите API ключ в настройках (шестеренка вверху AI).";
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

                string endpoint = baseUrl.TrimEnd('/');
                if (!endpoint.EndsWith("/audio/transcriptions"))
                {
                    endpoint += "/audio/transcriptions";
                }

                using var form = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(wavBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                form.Add(fileContent, "file", "voice.wav");
                form.Add(new StringContent("whisper-1"), "model");
                form.Add(new StringContent("ru"), "language");

                var response = await client.PostAsync(endpoint, form);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    App.Log($"Whisper API error ({response.StatusCode}): {json}");
                    return $"Ошибка распознавания ({response.StatusCode})";
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var textEl))
                {
                    return textEl.GetString()?.Trim() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                App.Log($"Whisper transcription exception: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }

            return string.Empty;
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
            _waveWriter?.Dispose();
            _audioStream?.Dispose();
        }
    }
}
