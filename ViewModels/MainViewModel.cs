using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Helpers;
using DynamicIsland.Models;
using DynamicIsland.Services;

namespace DynamicIsland.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly ICursorTrackerService _cursorTrackerService;
        private readonly IMediaService _mediaService;
        private readonly IAudioVisualizerService _audioVisualizerService;
        private readonly IHardwareMonitorService _hardwareMonitorService;
        private readonly ISystemNotificationService _notificationService;
        private readonly IClipboardService _clipboardService;
        private readonly ITimerService _timerService;

        private readonly DispatcherTimer _hudDismissTimer;

        [ObservableProperty]
        private IslandState _currentState = IslandState.Idle;

        [ObservableProperty]
        private DisplayMode _selectedMode = DisplayMode.Auto;

        [ObservableProperty]
        private bool _isClickThrough;

        // Глазки и зрачки (Idle)
        [ObservableProperty]
        private double _leftPupilX;

        [ObservableProperty]
        private double _leftPupilY;

        [ObservableProperty]
        private double _rightPupilX;

        [ObservableProperty]
        private double _rightPupilY;

        [ObservableProperty]
        private double _eyeScaleY = 1.0;

        // Медиа
        [ObservableProperty]
        private string _trackTitle = "Нет воспроизведения";

        [ObservableProperty]
        private string _trackArtist = "Ожидание аудио...";

        [ObservableProperty]
        private BitmapImage? _trackThumbnail;

        [ObservableProperty]
        private bool _isMediaPlaying;

        [ObservableProperty]
        private bool _hasMedia;

        // Эквалайзер (14 полос)
        public ObservableCollection<float> EqualizerBands { get; } = new();

        // Аппаратный мониторинг
        [ObservableProperty]
        private float _cpuUsage;

        [ObservableProperty]
        private float _cpuTemp;

        [ObservableProperty]
        private float _gpuUsage;

        [ObservableProperty]
        private float _gpuTemp;

        [ObservableProperty]
        private bool _isFullscreenGame;

        // Активный режим отображения для компактного вида: "Eyes", "Media", "Gaming", "Timer"
        [ObservableProperty]
        private string _activeCompactView = "Eyes";

        // HUD Уведомления (Громкость, CapsLock, USB, Буфер)
        [ObservableProperty]
        private HudNotification? _activeNotification;

        [ObservableProperty]
        private bool _isShowingNotification;

        // Фокус Таймер (Pomodoro)
        [ObservableProperty]
        private string _timerFormatted = "00:00";

        [ObservableProperty]
        private double _timerProgress;

        [ObservableProperty]
        private bool _isTimerRunning;

        [ObservableProperty]
        private bool _isTimerActive;

        // Буфер обмена
        public ObservableCollection<string> ClipboardHistory => _clipboardService.History;

        // Вкладка в раскрытом режиме: "Media", "Timer", "Clipboard", "Apps"
        [ObservableProperty]
        private string _expandedTab = "Media";

        // Автозагрузка
        [ObservableProperty]
        private bool _isAutoStartEnabled;

        // Размеры капсулы
        [ObservableProperty]
        private double _islandWidth = 180;

        [ObservableProperty]
        private double _islandHeight = 36;

        [ObservableProperty]
        private double _islandCornerRadius = 18;

        public event Action<bool>? RequestClickThroughChanged;

        public MainViewModel(
            ICursorTrackerService cursorTrackerService,
            IMediaService mediaService,
            IAudioVisualizerService audioVisualizerService,
            IHardwareMonitorService hardwareMonitorService,
            ISystemNotificationService notificationService,
            IClipboardService clipboardService,
            ITimerService timerService)
        {
            _cursorTrackerService = cursorTrackerService;
            _mediaService = mediaService;
            _audioVisualizerService = audioVisualizerService;
            _hardwareMonitorService = hardwareMonitorService;
            _notificationService = notificationService;
            _clipboardService = clipboardService;
            _timerService = timerService;

            for (int i = 0; i < 14; i++)
            {
                EqualizerBands.Add(0.15f);
            }

            _isAutoStartEnabled = AutoStartHelper.IsAutoStartEnabled();

            _hudDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            _hudDismissTimer.Tick += (s, e) => DismissNotification();

            // Подписки
            _cursorTrackerService.CursorMoved += OnCursorMoved;
            _mediaService.MediaChanged += OnMediaChanged;
            _audioVisualizerService.SpectrumUpdated += OnSpectrumUpdated;
            _hardwareMonitorService.MetricsUpdated += OnMetricsUpdated;
            _notificationService.NotificationTriggered += OnNotificationTriggered;
            _clipboardService.ItemCopied += OnClipboardItemCopied;
            _timerService.TimerTicked += OnTimerTicked;
            _timerService.TimerFinished += OnTimerFinished;

            // Запуск базовых сервисов
            _cursorTrackerService.Start();
            _audioVisualizerService.Start();
            _hardwareMonitorService.Start();
            _notificationService.Initialize();
            _ = _mediaService.InitializeAsync();

            UpdateStateAndDimensions();
        }

        private void OnCursorMoved(object? sender, CursorTrackingEventArgs e)
        {
            LeftPupilX = e.LeftPupilX;
            LeftPupilY = e.LeftPupilY;
            RightPupilX = e.RightPupilX;
            RightPupilY = e.RightPupilY;
            EyeScaleY = e.EyeScaleY;
        }

        private void OnMediaChanged(object? sender, TrackInfo track)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                TrackTitle = track.Title;
                TrackArtist = track.Artist;
                TrackThumbnail = track.Thumbnail;
                IsMediaPlaying = track.IsPlaying;
                HasMedia = track.HasMedia;

                EvaluateDisplayState();
            });
        }

        private void OnSpectrumUpdated(object? sender, float[] bands)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                int count = Math.Min(bands.Length, EqualizerBands.Count);
                for (int i = 0; i < count; i++)
                {
                    EqualizerBands[i] = bands[i];
                }
            });
        }

        private void OnMetricsUpdated(object? sender, HardwareMetrics metrics)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                CpuUsage = metrics.CpuUsage;
                CpuTemp = metrics.CpuTemp;
                GpuUsage = metrics.GpuUsage;
                GpuTemp = metrics.GpuTemp;
                IsFullscreenGame = metrics.IsFullscreenGameActive;

                EvaluateDisplayState();
            });
        }

        // ================= СИСТЕМНЫЕ УВЕДОМЛЕНИЯ =================

        private void OnNotificationTriggered(object? sender, HudNotification notification)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                // Не перебиваем раскрытый режим
                if (CurrentState == IslandState.Expanded) return;

                ActiveNotification = notification;
                IsShowingNotification = true;

                // Размер капсулы под HUD-уведомление
                IslandWidth = 240;
                IslandHeight = 40;
                IslandCornerRadius = 20;

                _hudDismissTimer.Stop();
                _hudDismissTimer.Interval = TimeSpan.FromMilliseconds(notification.DurationMs);
                _hudDismissTimer.Start();
            });
        }

        private void DismissNotification()
        {
            _hudDismissTimer.Stop();
            IsShowingNotification = false;
            ActiveNotification = null;
            UpdateDimensions();
        }

        private void OnClipboardItemCopied(object? sender, string preview)
        {
            _notificationService.TriggerCustomNotification(new HudNotification
            {
                Type = NotificationType.Clipboard,
                Title = "Скопировано в буфер",
                Subtitle = preview,
                ShowProgress = false,
                IconPathData = "M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z",
                AccentColor = "#BF5AF2",
                DurationMs = 1500
            });
        }

        // ================= ФОКУС-ТАЙМЕР =================

        private void OnTimerTicked(object? sender, EventArgs e)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                TimerFormatted = _timerService.FormattedTime;
                TimerProgress = _timerService.Progress;
                IsTimerRunning = _timerService.IsRunning;
                IsTimerActive = _timerService.IsActive;

                EvaluateDisplayState();
            });
        }

        private void OnTimerFinished(object? sender, EventArgs e)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                _notificationService.TriggerCustomNotification(new HudNotification
                {
                    Type = NotificationType.TimerFinished,
                    Title = "Время вышло!",
                    Subtitle = "Фокус-сессия завершена",
                    ShowProgress = false,
                    IconPathData = "M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm4.2 14.2L11 13V7h1.5v5.2l4.5 2.7-.8 1.3z",
                    AccentColor = "#FF375F",
                    DurationMs = 3000
                });
            });
        }

        [RelayCommand]
        public void StartTimer(string minutesStr)
        {
            if (int.TryParse(minutesStr, out int mins))
            {
                _timerService.Start(TimeSpan.FromMinutes(mins));
            }
        }

        [RelayCommand]
        public void ToggleTimer()
        {
            if (_timerService.IsRunning)
            {
                _timerService.Pause();
            }
            else if (_timerService.IsActive)
            {
                _timerService.Resume();
            }
            else
            {
                _timerService.Start(TimeSpan.FromMinutes(25)); // По умолчанию 25 мин
            }
        }

        [RelayCommand]
        public void ResetTimer()
        {
            _timerService.Reset();
        }

        [RelayCommand]
        public void AddTimerMinutes(string minutesStr)
        {
            if (int.TryParse(minutesStr, out int mins))
            {
                _timerService.AddMinutes(mins);
            }
        }

        // ================= БУФЕР ОБМЕНА =================

        [RelayCommand]
        public void CopyClipboardItem(string text)
        {
            _clipboardService.CopyToClipboard(text);
        }

        [RelayCommand]
        public void ClearClipboard()
        {
            _clipboardService.ClearHistory();
        }

        // ================= БЫСТРЫЙ ЗАПУСК (DOCK) =================

        [RelayCommand]
        public void LaunchApp(string appKey)
        {
            try
            {
                switch (appKey.ToLowerInvariant())
                {
                    case "browser":
                        Process.Start(new ProcessStartInfo("https://google.com") { UseShellExecute = true });
                        break;
                    case "explorer":
                        Process.Start("explorer.exe");
                        break;
                    case "notepad":
                        Process.Start("notepad.exe");
                        break;
                    case "terminal":
                        try { Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true }); }
                        catch { Process.Start("cmd.exe"); }
                        break;
                    case "taskmgr":
                        Process.Start("taskmgr.exe");
                        break;
                    case "settings":
                        Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LaunchApp] Error: {ex.Message}");
            }
        }

        [RelayCommand]
        public void ToggleAutoStart()
        {
            IsAutoStartEnabled = !IsAutoStartEnabled;
            AutoStartHelper.SetAutoStart(IsAutoStartEnabled);
        }

        [RelayCommand]
        public void SetExpandedTab(string tab)
        {
            ExpandedTab = tab;
        }

        // ================= ЛОГИКА СОСТОЯНИЙ =================

        private void EvaluateDisplayState()
        {
            if (IsShowingNotification) return;
            if (CurrentState == IslandState.Expanded || CurrentState == IslandState.Minimized)
                return;

            string viewMode = "Eyes";

            switch (SelectedMode)
            {
                case DisplayMode.Eyes:
                    viewMode = "Eyes";
                    break;
                case DisplayMode.Media:
                    viewMode = "Media";
                    break;
                case DisplayMode.Gaming:
                    viewMode = "Gaming";
                    break;
                case DisplayMode.Auto:
                default:
                    if (IsFullscreenGame)
                    {
                        viewMode = "Gaming";
                    }
                    else if (IsMediaPlaying || HasMedia)
                    {
                        viewMode = "Media";
                    }
                    else
                    {
                        viewMode = "Eyes";
                    }
                    break;
            }

            ActiveCompactView = viewMode;

            if (viewMode == "Eyes")
            {
                CurrentState = IslandState.Idle;
            }
            else
            {
                CurrentState = IslandState.MediaOrGaming;
            }

            UpdateDimensions();
        }

        private void UpdateDimensions()
        {
            if (IsShowingNotification)
            {
                IslandWidth = 240;
                IslandHeight = 40;
                IslandCornerRadius = 20;
                return;
            }

            switch (CurrentState)
            {
                case IslandState.Minimized:
                    IslandWidth = 76;
                    IslandHeight = 6;
                    IslandCornerRadius = 3;
                    break;
                case IslandState.Idle:
                    IslandWidth = IsTimerActive ? 210 : 180;
                    IslandHeight = 36;
                    IslandCornerRadius = 18;
                    break;
                case IslandState.MediaOrGaming:
                    IslandWidth = IsTimerActive ? 310 : 280;
                    IslandHeight = 44;
                    IslandCornerRadius = 22;
                    break;
                case IslandState.Expanded:
                    IslandWidth = 440;
                    IslandHeight = 220;
                    IslandCornerRadius = 26;
                    break;
            }
        }

        public void UpdateStateAndDimensions()
        {
            EvaluateDisplayState();
        }

        [RelayCommand]
        public void ToggleExpand()
        {
            if (CurrentState == IslandState.Minimized)
            {
                RestoreFromMinimized();
                return;
            }

            if (CurrentState == IslandState.Expanded)
            {
                CurrentState = IslandState.Idle;
                EvaluateDisplayState();
            }
            else
            {
                DismissNotification();
                CurrentState = IslandState.Expanded;
                UpdateDimensions();
            }
        }

        [RelayCommand]
        public void MinimizeToBar()
        {
            DismissNotification();
            CurrentState = IslandState.Minimized;
            UpdateDimensions();
        }

        [RelayCommand]
        public void RestoreFromMinimized()
        {
            if (CurrentState == IslandState.Minimized)
            {
                CurrentState = IslandState.Idle;
                EvaluateDisplayState();
            }
        }

        [RelayCommand]
        public void Collapse()
        {
            if (CurrentState == IslandState.Expanded)
            {
                EvaluateDisplayState();
            }
        }

        [RelayCommand]
        public void SetMode(string modeStr)
        {
            if (Enum.TryParse<DisplayMode>(modeStr, true, out var mode))
            {
                SelectedMode = mode;
                EvaluateDisplayState();
            }
        }

        [RelayCommand]
        public async Task TogglePlayPauseAsync()
        {
            await _mediaService.TogglePlayPauseAsync();
        }

        [RelayCommand]
        public async Task SkipNextAsync()
        {
            await _mediaService.SkipNextAsync();
        }

        [RelayCommand]
        public async Task SkipPreviousAsync()
        {
            await _mediaService.SkipPreviousAsync();
        }

        [RelayCommand]
        public void ToggleClickThrough()
        {
            IsClickThrough = !IsClickThrough;
            RequestClickThroughChanged?.Invoke(IsClickThrough);
        }

        public void SetClickThroughExternal(bool enabled)
        {
            IsClickThrough = enabled;
            RequestClickThroughChanged?.Invoke(IsClickThrough);
        }

        [RelayCommand]
        public void CloseApp()
        {
            App.Current?.Shutdown();
        }

        public void UpdateScreenPosition(double screenCenterX, double screenTopY)
        {
            _cursorTrackerService.UpdateIslandScreenPosition(screenCenterX, screenTopY);
        }

        public void Dispose()
        {
            _cursorTrackerService.CursorMoved -= OnCursorMoved;
            _mediaService.MediaChanged -= OnMediaChanged;
            _audioVisualizerService.SpectrumUpdated -= OnSpectrumUpdated;
            _hardwareMonitorService.MetricsUpdated -= OnMetricsUpdated;
            _notificationService.NotificationTriggered -= OnNotificationTriggered;
            _clipboardService.ItemCopied -= OnClipboardItemCopied;
            _timerService.TimerTicked -= OnTimerTicked;
            _timerService.TimerFinished -= OnTimerFinished;

            _cursorTrackerService.Dispose();
            _audioVisualizerService.Dispose();
            _hardwareMonitorService.Dispose();
            _mediaService.Dispose();
            _notificationService.Dispose();
            _clipboardService.Dispose();
            _timerService.Dispose();
            _hudDismissTimer.Stop();
        }
    }
}
