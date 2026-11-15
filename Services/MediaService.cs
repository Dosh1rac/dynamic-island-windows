using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public class MediaService : IMediaService
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private TrackInfo _currentTrack = new();

        public event EventHandler<TrackInfo>? MediaChanged;
        public TrackInfo CurrentTrack => _currentTrack;

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                    UpdateCurrentSession();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaService] Ошибка инициализации SMTC: {ex.Message}");
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            UpdateCurrentSession();
        }

        private void UpdateCurrentSession()
        {
            if (_sessionManager == null) return;

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            try
            {
                _currentSession = _sessionManager.GetCurrentSession();
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaService] Ошибка получения сессии: {ex.Message}");
            }

            _ = RefreshMediaInfoAsync();
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = RefreshMediaInfoAsync();
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _ = RefreshMediaInfoAsync();
        }

        private async Task RefreshMediaInfoAsync()
        {
            try
            {
                if (_currentSession == null)
                {
                    _currentTrack = new TrackInfo
                    {
                        Title = "Нет воспроизведения",
                        Artist = "Ожидание медиа...",
                        HasMedia = false,
                        IsPlaying = false,
                        Thumbnail = null
                    };
                    MediaChanged?.Invoke(this, _currentTrack);
                    return;
                }

                var playbackInfo = _currentSession.GetPlaybackInfo();
                bool isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
                if (mediaProperties == null)
                {
                    _currentTrack = new TrackInfo
                    {
                        Title = "Медиаплеер активен",
                        Artist = "Трек не определен",
                        HasMedia = true,
                        IsPlaying = isPlaying,
                        Thumbnail = null
                    };
                    MediaChanged?.Invoke(this, _currentTrack);
                    return;
                }

                BitmapImage? thumbnail = null;
                if (mediaProperties.Thumbnail != null)
                {
                    try
                    {
                        using IRandomAccessStreamWithContentType stream = await mediaProperties.Thumbnail.OpenReadAsync();
                        using Stream netStream = stream.AsStreamForRead();
                        using MemoryStream memStream = new MemoryStream();
                        await netStream.CopyToAsync(memStream);
                        memStream.Position = 0;

                        thumbnail = new BitmapImage();
                        thumbnail.BeginInit();
                        thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                        thumbnail.StreamSource = memStream;
                        thumbnail.EndInit();
                        thumbnail.Freeze();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MediaService] Не удалось загрузить обложку: {ex.Message}");
                    }
                }

                string title = string.IsNullOrWhiteSpace(mediaProperties.Title) ? "Неизвестный трек" : mediaProperties.Title;
                string artist = string.IsNullOrWhiteSpace(mediaProperties.Artist) ? "Неизвестный исполнитель" : mediaProperties.Artist;

                _currentTrack = new TrackInfo
                {
                    Title = title,
                    Artist = artist,
                    Album = mediaProperties.AlbumTitle ?? string.Empty,
                    Thumbnail = thumbnail,
                    IsPlaying = isPlaying,
                    HasMedia = true
                };

                MediaChanged?.Invoke(this, _currentTrack);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaService] Ошибка обновления информации о треке: {ex.Message}");
            }
        }

        public async Task TogglePlayPauseAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TryTogglePlayPauseAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaService] PlayPause error: {ex.Message}");
                }
            }
        }

        public async Task SkipNextAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TrySkipNextAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaService] SkipNext error: {ex.Message}");
                }
            }
        }

        public async Task SkipPreviousAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TrySkipPreviousAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaService] SkipPrevious error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }
        }
    }
}
