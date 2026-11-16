using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Helpers;
using DynamicIsland.Models;
using DynamicIsland.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

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
        private readonly ISoundEffectService _soundEffectService;
        private readonly IDropZoneService _dropZoneService;
        private readonly IWeatherService _weatherService;
        private readonly IAiAgentService _aiAgentService;
        private readonly IVoiceService _voiceService;
        private readonly IThemeService _themeService;

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

        [ObservableProperty]
        private string _trackPositionFormatted = "00:00";

        [ObservableProperty]
        private string _trackDurationFormatted = "00:00";

        [ObservableProperty]
        private double _trackProgress;

        [ObservableProperty]
        private Color _ambientGlowColor = Color.FromArgb(0x35, 0x0A, 0x84, 0xFF);

        private TimeSpan _currentTrackPosition = TimeSpan.Zero;
        private TimeSpan _currentTrackDuration = TimeSpan.Zero;
        private readonly DispatcherTimer _playbackTimer;

        // Эквалайзер (14 полос)
        public ObservableCollection<float> EqualizerBands { get; } = new();

        // Аппаратный мониторинг
        [ObservableProperty]
        private float _cpuUsage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCpuTemp))]
        private float _cpuTemp;

        [ObservableProperty]
        private float _gpuUsage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasGpuTemp))]
        private float _gpuTemp;

        public bool HasCpuTemp => CpuTemp > 0;
        public bool HasGpuTemp => GpuTemp > 0;

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

        // Drop Zone файлы
        public ObservableCollection<DroppedFileInfo> DroppedFiles => _dropZoneService.Files;

        [ObservableProperty]
        private bool _isDragOverIsland;

        // Погода
        [ObservableProperty]
        private string _weatherTemp = "+12°";

        [ObservableProperty]
        private string _weatherDesc = "Ясно";

        [ObservableProperty]
        private string _weatherIcon = "M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5z";

        [ObservableProperty]
        private string _weatherCity = "Москва";

        [ObservableProperty]
        private string _weatherCountry = "Россия";

        [ObservableProperty]
        private bool _isWeatherAutoLocation = true;

        [ObservableProperty]
        private bool _isCityPickerOpen;

        [ObservableProperty]
        private string _citySearchInput = string.Empty;

        [ObservableProperty]
        private string _cityPickerStatusMessage = string.Empty;

        public string WeatherCityAndTemp => $"{WeatherCity}: {WeatherTemp}";

        public string WeatherFullTooltip => $"{WeatherCity} ({WeatherCountry}): {WeatherDesc} {WeatherTemp}\n{(IsWeatherAutoLocation ? "📍 Режим: Авто по IP (нажмите для смены)" : "📍 Режим: Выбран вручную (нажмите для смены)")}";

        // Калькулятор Spotlight
        [ObservableProperty]
        private string _calcQuery = string.Empty;

        [ObservableProperty]
        private string _calcResult = string.Empty;

        // Вкладка в раскрытом режиме: "Media", "Timer", "Clipboard", "Apps", "DropZone", "Tools"
        [ObservableProperty]
        private string _expandedTab = "Media";

        // Автозагрузка и звуки
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutoStartStatusText))]
        private bool _isAutoStartEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SoundStatusText))]
        private bool _isSoundEnabled = true;

        public string AutoStartStatusText => IsAutoStartEnabled ? "Автозапуск: Вкл" : "Автозапуск: Выкл";
        public string SoundStatusText => IsSoundEnabled ? "Звук: Вкл" : "Звук: Выкл";

        // ================= AI АГЕНТ И ГОЛОС =================
        public ObservableCollection<ChatMessageItem> ChatMessages { get; } = new();

        [ObservableProperty]
        private string _agentUserInput = string.Empty;

        [ObservableProperty]
        private bool _isAgentBusy;

        [ObservableProperty]
        private string _agentBusyStatus = "Думаю...";

        [ObservableProperty]
        private bool _isVoiceRecording;

        [ObservableProperty]
        private bool _isAiSettingsOpen;

        [ObservableProperty]
        private string _aiSettingsBaseUrl = string.Empty;

        [ObservableProperty]
        private string _aiSettingsApiKey = string.Empty;

        [ObservableProperty]
        private string _aiSettingsModel = string.Empty;

        [ObservableProperty]
        private bool _aiSettingsEnableControl = true;

        [ObservableProperty]
        private string _aiStatusSummary = "OpenAI (gpt-4o-mini)";

        // Размеры капсулы
        [ObservableProperty]
        private double _islandWidth = 180;

        [ObservableProperty]
        private double _islandHeight = 36;

        [ObservableProperty]
        private double _islandCornerRadius = 18;

        // ================= ОФОРМЛЕНИЕ И ЖИДКОЕ СТЕКЛО =================
        [ObservableProperty]
        private IslandTheme _currentTheme = IslandTheme.LiquidGlassDark;

        [ObservableProperty]
        private bool _enableLiquidWaves = true;

        [ObservableProperty]
        private bool _isThemePickerOpen;

        [ObservableProperty]
        private string _themeDisplayShortName = "🎨 Стекло";

        [ObservableProperty]
        private string _themeDisplayName = "Жидкое стекло (Тёмное)";

        [ObservableProperty]
        private Brush _capsuleBackgroundBrush = Brushes.Black;

        [ObservableProperty]
        private Brush _capsuleBorderBrush = Brushes.Transparent;

        [ObservableProperty]
        private Thickness _capsuleBorderThickness = new Thickness(1.2);

        [ObservableProperty]
        private Brush _themeForegroundBrush = Brushes.White;

        [ObservableProperty]
        private Brush _themeSecondaryForegroundBrush = Brushes.Gray;

        [ObservableProperty]
        private Brush _themeControlBackgroundBrush = Brushes.DarkGray;

        [ObservableProperty]
        private Brush _themeSubContainerBrush = Brushes.Black;

        [ObservableProperty]
        private Brush _themeBorderBrush = Brushes.Gray;

        [ObservableProperty]
        private bool _isDarkTheme = true;

        public event Action<bool>? RequestClickThroughChanged;

        public MainViewModel(
            ICursorTrackerService cursorTrackerService,
            IMediaService mediaService,
            IAudioVisualizerService audioVisualizerService,
            IHardwareMonitorService hardwareMonitorService,
            ISystemNotificationService notificationService,
            IClipboardService clipboardService,
            ITimerService timerService,
            ISoundEffectService soundEffectService,
            IDropZoneService dropZoneService,
            IWeatherService weatherService,
            IAiAgentService aiAgentService,
            IVoiceService voiceService,
            IThemeService themeService)
        {
            _cursorTrackerService = cursorTrackerService;
            _mediaService = mediaService;
            _audioVisualizerService = audioVisualizerService;
            _hardwareMonitorService = hardwareMonitorService;
            _notificationService = notificationService;
            _clipboardService = clipboardService;
            _timerService = timerService;
            _soundEffectService = soundEffectService;
            _dropZoneService = dropZoneService;
            _weatherService = weatherService;
            _aiAgentService = aiAgentService;
            _voiceService = voiceService;
            _themeService = themeService;

            var themeSettings = _themeService.Settings;
            _currentTheme = themeSettings.Theme;
            _enableLiquidWaves = themeSettings.EnableLiquidWaves;
            ApplyTheme(_currentTheme, _enableLiquidWaves, save: false);

            var cfg = _aiAgentService.Config;
            AiSettingsBaseUrl = cfg.BaseUrl;
            AiSettingsApiKey = cfg.ApiKey;
            AiSettingsModel = cfg.Model;
            AiSettingsEnableControl = cfg.EnableComputerControl;
            AiStatusSummary = $"{cfg.Model}";

            ChatMessages.Add(new ChatMessageItem
            {
                Role = "assistant",
                Content = "Привет! Я твой AI-ассистент в Dynamic Island. Могу отвечать на вопросы, запускать программы, выполнять скрипты PowerShell, делать скриншоты или регулировать звук. Нажми 🎙️ для голосового ввода!"
            });

            for (int i = 0; i < 14; i++)
            {
                EqualizerBands.Add(0.15f);
            }

            _isAutoStartEnabled = AutoStartHelper.IsAutoStartEnabled();

            _hudDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            _hudDismissTimer.Tick += (s, e) => DismissNotification();

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _playbackTimer.Tick += (s, e) =>
            {
                if (IsMediaPlaying && _currentTrackDuration > TimeSpan.Zero)
                {
                    _currentTrackPosition = _currentTrackPosition.Add(TimeSpan.FromSeconds(1));
                    if (_currentTrackPosition > _currentTrackDuration)
                        _currentTrackPosition = _currentTrackDuration;
                    UpdatePlaybackTimes();
                }
            };
            _playbackTimer.Start();

            // Подписки
            _cursorTrackerService.CursorMoved += OnCursorMoved;
            _mediaService.MediaChanged += OnMediaChanged;
            _audioVisualizerService.SpectrumUpdated += OnSpectrumUpdated;
            _hardwareMonitorService.MetricsUpdated += OnMetricsUpdated;
            _notificationService.NotificationTriggered += OnNotificationTriggered;
            _clipboardService.ItemCopied += OnClipboardItemCopied;
            _timerService.TimerTicked += OnTimerTicked;
            _timerService.TimerFinished += OnTimerFinished;
            _weatherService.WeatherUpdated += OnWeatherUpdated;
            _dropZoneService.FileAdded += OnFileAddedToDropZone;

            // Запуск базовых сервисов
            _cursorTrackerService.Start();
            _audioVisualizerService.Start();
            _hardwareMonitorService.Start();
            _notificationService.Initialize();
            _weatherService.Start();
            WeatherCity = _weatherService.CurrentCity;
            WeatherCountry = _weatherService.CurrentCountry;
            IsWeatherAutoLocation = _weatherService.IsAutoLocation;
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
                AmbientGlowColor = track.AccentColor;
                _currentTrackPosition = track.Position;
                _currentTrackDuration = track.Duration;
                UpdatePlaybackTimes();

                EvaluateDisplayState();
            });
        }

        private void UpdatePlaybackTimes()
        {
            TrackPositionFormatted = _currentTrackPosition.ToString(@"mm\:ss");
            TrackDurationFormatted = _currentTrackDuration > TimeSpan.Zero
                ? _currentTrackDuration.ToString(@"mm\:ss")
                : "--:--";
            TrackProgress = _currentTrackDuration.TotalSeconds > 0
                ? Math.Clamp(_currentTrackPosition.TotalSeconds / _currentTrackDuration.TotalSeconds, 0.0, 1.0)
                : 0.0;
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

                OnPropertyChanged(nameof(HasCpuTemp));
                OnPropertyChanged(nameof(HasGpuTemp));

                EvaluateDisplayState();
            });
        }

        private void OnWeatherUpdated(object? sender, EventArgs e)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                WeatherTemp = _weatherService.CurrentTemperature;
                WeatherDesc = _weatherService.WeatherDescription;
                WeatherIcon = _weatherService.WeatherIconPathData;
                WeatherCity = _weatherService.CurrentCity;
                WeatherCountry = _weatherService.CurrentCountry;
                IsWeatherAutoLocation = _weatherService.IsAutoLocation;
                OnPropertyChanged(nameof(WeatherCityAndTemp));
                OnPropertyChanged(nameof(WeatherFullTooltip));
            });
        }

        // ================= ВЫБОР ГОРОДА ПОГОДЫ =================

        [RelayCommand]
        public void ToggleCityPicker()
        {
            IsCityPickerOpen = !IsCityPickerOpen;
            if (IsCityPickerOpen)
            {
                CityPickerStatusMessage = IsWeatherAutoLocation
                    ? $"Текущий: {WeatherCity} (Авто по IP)"
                    : $"Текущий: {WeatherCity} (Задан вручную)";
            }
        }

        [RelayCommand]
        public void CloseCityPicker()
        {
            IsCityPickerOpen = false;
        }

        [RelayCommand]
        public async Task ApplyCitySearch()
        {
            if (string.IsNullOrWhiteSpace(CitySearchInput)) return;

            CityPickerStatusMessage = "Поиск города...";
            bool success = await _weatherService.SetManualCityAsync(CitySearchInput);
            if (success)
            {
                CityPickerStatusMessage = $"✓ Установлен город: {_weatherService.CurrentCity}";
                _soundEffectService.PlaySnap();
                await Task.Delay(900);
                IsCityPickerOpen = false;
            }
            else
            {
                CityPickerStatusMessage = "Город не найден, попробуйте иначе";
            }
        }

        [RelayCommand]
        public async Task SetCityPreset(string presetCity)
        {
            CityPickerStatusMessage = $"Применяем {presetCity}...";
            bool success = await _weatherService.SetManualCityAsync(presetCity);
            if (success)
            {
                CityPickerStatusMessage = $"✓ Установлен город: {_weatherService.CurrentCity}";
                _soundEffectService.PlaySnap();
                await Task.Delay(900);
                IsCityPickerOpen = false;
            }
        }

        [RelayCommand]
        public async Task SetAutoCity()
        {
            CityPickerStatusMessage = "Определение по IP...";
            await _weatherService.SetAutoLocationAsync();
            CityPickerStatusMessage = $"✓ Авто по IP: {_weatherService.CurrentCity}";
            _soundEffectService.PlaySnap();
            await Task.Delay(900);
            IsCityPickerOpen = false;
        }

        // ================= СИСТЕМНЫЕ УВЕДОМЛЕНИЯ =================

        private void OnNotificationTriggered(object? sender, HudNotification notification)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                if (CurrentState == IslandState.Expanded) return;

                ActiveNotification = notification;
                IsShowingNotification = true;
                _soundEffectService.PlayPop();

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

        // ================= DROP ZONE (AIRDROP КАРМАН) =================

        public void HandleFilesDropped(string[] paths)
        {
            _dropZoneService.AddFiles(paths);
            _soundEffectService.PlayDrop();
            IsDragOverIsland = false;

            _notificationService.TriggerCustomNotification(new HudNotification
            {
                Type = NotificationType.UsbConnected,
                Title = "Файлы сохранены в карман",
                Subtitle = $"Добавлено: {paths.Length} шт.",
                ShowProgress = false,
                IconPathData = "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z",
                AccentColor = "#30D158",
                DurationMs = 2000
            });
        }

        private void OnFileAddedToDropZone(object? sender, DroppedFileInfo file)
        {
            // Открываем вкладку DropZone при раскрытии
            ExpandedTab = "DropZone";
        }

        [RelayCommand]
        public void RemoveDroppedFile(DroppedFileInfo file)
        {
            _dropZoneService.RemoveFile(file);
        }

        [RelayCommand]
        public void ClearDropZone()
        {
            _dropZoneService.Clear();
        }

        [RelayCommand]
        public void CopyFilePath(DroppedFileInfo file)
        {
            _clipboardService.CopyToClipboard(file.FilePath);
        }

        // ================= КАЛЬКУЛЯТОР =================

        partial void OnCalcQueryChanged(string value)
        {
            CalcResult = MiniCalculator.Evaluate(value);
        }

        [RelayCommand]
        public void CopyCalcResult()
        {
            if (!string.IsNullOrWhiteSpace(CalcResult))
            {
                _clipboardService.CopyToClipboard(CalcResult);
            }
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
                _soundEffectService.PlayPop();
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
                _soundEffectService.PlayPop();
                _timerService.Start(TimeSpan.FromMinutes(mins));
            }
        }

        [RelayCommand]
        public void ToggleTimer()
        {
            _soundEffectService.PlayPop();
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
                _timerService.Start(TimeSpan.FromMinutes(25));
            }
        }

        [RelayCommand]
        public void ResetTimer()
        {
            _soundEffectService.PlaySnap();
            _timerService.Reset();
        }

        [RelayCommand]
        public void AddTimerMinutes(string minutesStr)
        {
            if (int.TryParse(minutesStr, out int mins))
            {
                _soundEffectService.PlayPop();
                _timerService.AddMinutes(mins);
            }
        }

        // ================= БУФЕР ОБМЕНА =================

        [RelayCommand]
        public void CopyClipboardItem(string text)
        {
            _soundEffectService.PlaySnap();
            _clipboardService.CopyToClipboard(text);
        }

        [RelayCommand]
        public void ClearClipboard()
        {
            _clipboardService.ClearHistory();
        }

        // ================= ДОК БЫСТРОГО ЗАПУСКА =================

        [RelayCommand]
        public void LaunchApp(string appKey)
        {
            _soundEffectService.PlayPop();
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
            _soundEffectService.PlayPop();
            IsAutoStartEnabled = !IsAutoStartEnabled;
            AutoStartHelper.SetAutoStart(IsAutoStartEnabled);
        }

        [RelayCommand]
        public void ToggleSound()
        {
            IsSoundEnabled = !IsSoundEnabled;
            _soundEffectService.IsSoundEnabled = IsSoundEnabled;
            if (IsSoundEnabled) _soundEffectService.PlayPop();
        }

        [RelayCommand]
        public void SetExpandedTab(string tab)
        {
            _soundEffectService.PlayPop();
            ExpandedTab = tab;
            UpdateDimensions();
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
                    if (string.Equals(ExpandedTab, "AI", StringComparison.OrdinalIgnoreCase))
                    {
                        IslandWidth = 640;
                        IslandHeight = 440;
                        IslandCornerRadius = 32;
                    }
                    else
                    {
                        IslandWidth = 460;
                        IslandHeight = 240;
                        IslandCornerRadius = 26;
                    }
                    break;
            }
        }

        public void UpdateStateAndDimensions()
        {
            EvaluateDisplayState();
        }

        [RelayCommand]
        public void ExpandIsland()
        {
            if (CurrentState != IslandState.Expanded)
            {
                DismissNotification();
                _soundEffectService.PlayPop();
                CurrentState = IslandState.Expanded;
                UpdateStateAndDimensions();
            }
        }

        [RelayCommand]
        public void ToggleExpand()
        {
            if (CurrentState == IslandState.Minimized)
            {
                _soundEffectService.PlayPop();
                RestoreFromMinimized();
                return;
            }

            if (CurrentState == IslandState.Expanded)
            {
                _soundEffectService.PlaySnap();
                CurrentState = IslandState.Idle;
                EvaluateDisplayState();
            }
            else
            {
                _soundEffectService.PlayPop();
                DismissNotification();
                CurrentState = IslandState.Expanded;
                UpdateDimensions();
            }
        }

        [RelayCommand]
        public void MinimizeToBar()
        {
            _soundEffectService.PlaySnap();
            DismissNotification();
            CurrentState = IslandState.Minimized;
            UpdateDimensions();
        }

        [RelayCommand]
        public void RestoreFromMinimized()
        {
            if (CurrentState == IslandState.Minimized)
            {
                _soundEffectService.PlayPop();
                CurrentState = IslandState.Idle;
                EvaluateDisplayState();
            }
        }

        [RelayCommand]
        public void Collapse()
        {
            if (CurrentState == IslandState.Expanded)
            {
                _soundEffectService.PlaySnap();
                EvaluateDisplayState();
            }
        }

        [RelayCommand]
        public void SetMode(string modeStr)
        {
            _soundEffectService.PlayPop();
            if (Enum.TryParse<DisplayMode>(modeStr, true, out var mode))
            {
                SelectedMode = mode;
                EvaluateDisplayState();
            }
        }

        [RelayCommand]
        public async Task TogglePlayPauseAsync()
        {
            _soundEffectService.PlayPop();
            await _mediaService.TogglePlayPauseAsync();
        }

        [RelayCommand]
        public async Task SkipNextAsync()
        {
            _soundEffectService.PlayPop();
            await _mediaService.SkipNextAsync();
        }

        [RelayCommand]
        public async Task SkipPreviousAsync()
        {
            _soundEffectService.PlayPop();
            await _mediaService.SkipPreviousAsync();
        }

        [RelayCommand]
        public void ToggleClickThrough()
        {
            _soundEffectService.PlayPop();
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

        // ================= AI АГЕНТ И ГОЛОСОВОЕ УПРАВЛЕНИЕ =================

        [RelayCommand]
        public async Task SendAgentMessage()
        {
            if (string.IsNullOrWhiteSpace(AgentUserInput) || IsAgentBusy) return;

            string text = AgentUserInput.Trim();
            AgentUserInput = string.Empty;

            ChatMessages.Add(new ChatMessageItem
            {
                Role = "user",
                Content = text
            });

            IsAgentBusy = true;
            AgentBusyStatus = "Думаю...";

            var result = await _aiAgentService.SendMessageAsync(
                new System.Collections.Generic.List<ChatMessageItem>(ChatMessages),
                text,
                status => App.Current?.Dispatcher.Invoke(() => AgentBusyStatus = status)
            );

            IsAgentBusy = false;

            if (result.Success)
            {
                ChatMessages.Add(new ChatMessageItem
                {
                    Role = "assistant",
                    Content = result.ReplyText,
                    ActionBadge = string.IsNullOrEmpty(result.ExecutedAction) ? "" : $"⚡ {result.ExecutedAction}"
                });
            }
            else
            {
                ChatMessages.Add(new ChatMessageItem
                {
                    Role = "assistant",
                    Content = $"⚠ {result.ErrorMessage}"
                });
            }
        }

        [RelayCommand]
        public async Task ToggleVoiceRecording()
        {
            if (!IsVoiceRecording)
            {
                _voiceService.StartRecording();
                IsVoiceRecording = true;
                _soundEffectService.PlayPop();
            }
            else
            {
                IsVoiceRecording = false;
                _soundEffectService.PlaySnap();
                IsAgentBusy = true;
                AgentBusyStatus = "Распознаю голос...";

                string transcribed = await _voiceService.StopRecordingAndTranscribeAsync(_aiAgentService.Config.ApiKey, _aiAgentService.Config.BaseUrl);
                IsAgentBusy = false;

                if (!string.IsNullOrWhiteSpace(transcribed))
                {
                    AgentUserInput = transcribed;
                    await SendAgentMessage();
                }
            }
        }

        [RelayCommand]
        public void ClearAgentChat()
        {
            ChatMessages.Clear();
            ChatMessages.Add(new ChatMessageItem
            {
                Role = "assistant",
                Content = "Чат очищен. Чем могу помочь?"
            });
        }

        [RelayCommand]
        public void OpenAiSettings()
        {
            var cfg = _aiAgentService.Config;
            AiSettingsBaseUrl = cfg.BaseUrl;
            AiSettingsApiKey = cfg.ApiKey;
            AiSettingsModel = cfg.Model;
            AiSettingsEnableControl = cfg.EnableComputerControl;
            IsAiSettingsOpen = true;
        }

        [RelayCommand]
        public void CloseAiSettings()
        {
            IsAiSettingsOpen = false;
        }

        [RelayCommand]
        public void SaveAiSettings()
        {
            var cfg = _aiAgentService.Config;
            cfg.BaseUrl = string.IsNullOrWhiteSpace(AiSettingsBaseUrl) ? "https://api.openai.com/v1" : AiSettingsBaseUrl.Trim();
            cfg.ApiKey = AiSettingsApiKey?.Trim() ?? string.Empty;
            cfg.Model = string.IsNullOrWhiteSpace(AiSettingsModel) ? "gpt-4o-mini" : AiSettingsModel.Trim();
            cfg.EnableComputerControl = AiSettingsEnableControl;

            _aiAgentService.SaveConfig(cfg);
            AiStatusSummary = $"{cfg.Model}";
            _soundEffectService.PlaySnap();
            IsAiSettingsOpen = false;
        }

        [RelayCommand]
        public void ApplyAiPreset(string preset)
        {
            switch (preset)
            {
                case "Tooken":
                case "Tooken Club":
                    AiSettingsBaseUrl = "https://tooken.club/v1";
                    AiSettingsModel = "gpt-6-luna";
                    break;
                case "OpenAI":
                    AiSettingsBaseUrl = "https://api.openai.com/v1";
                    AiSettingsModel = "gpt-4o-mini";
                    break;
                case "DeepSeek":
                    AiSettingsBaseUrl = "https://api.deepseek.com/v1";
                    AiSettingsModel = "deepseek-chat";
                    break;
                case "OpenRouter":
                    AiSettingsBaseUrl = "https://openrouter.ai/api/v1";
                    AiSettingsModel = "google/gemini-2.0-flash-exp:free";
                    break;
                case "Gemini":
                    AiSettingsBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
                    AiSettingsModel = "gemini-2.0-flash";
                    break;
                case "Local":
                    AiSettingsBaseUrl = "http://localhost:11434/v1";
                    AiSettingsModel = "llama3";
                    break;
            }
        }

        [RelayCommand]
        public async Task SendQuickPrompt(string prompt)
        {
            AgentUserInput = prompt;
            await SendAgentMessage();
        }

        public void ApplyTheme(IslandTheme theme, bool enableWaves, bool save = true)
        {
            CurrentTheme = theme;
            EnableLiquidWaves = enableWaves;

            // 1px Rim Light (тонкий контур от 20% белого сверху до 5% белого снизу)
            var rimLight = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            rimLight.GradientStops.Add(new GradientStop(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF), 0.0));
            rimLight.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF), 0.5));
            rimLight.GradientStops.Add(new GradientStop(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF), 1.0));

            switch (theme)
            {
                case IslandTheme.Light:
                    CapsuleBackgroundBrush = new SolidColorBrush(Color.FromArgb(0xF4, 0xF4, 0xF4, 0xF6));
                    CapsuleBorderBrush = new SolidColorBrush(Color.FromArgb(0x25, 0x00, 0x00, 0x00));
                    CapsuleBorderThickness = new Thickness(1.0);
                    ThemeForegroundBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B));
                    ThemeSecondaryForegroundBrush = new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A));
                    ThemeControlBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE7));
                    ThemeSubContainerBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE7));
                    ThemeBorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD8));
                    IsDarkTheme = false;
                    ThemeDisplayShortName = "☀️ Светлая";
                    ThemeDisplayName = "Светлая (#F4F4F6)";
                    break;

                case IslandTheme.LiquidGlassLight:
                    CapsuleBackgroundBrush = new SolidColorBrush(Color.FromArgb(0xE0, 0xEE, 0xF2, 0xF8));
                    CapsuleBorderBrush = rimLight;
                    CapsuleBorderThickness = new Thickness(1.0);
                    ThemeForegroundBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B));
                    ThemeSecondaryForegroundBrush = new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A));
                    ThemeControlBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
                    ThemeSubContainerBrush = new SolidColorBrush(Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF));
                    ThemeBorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x90, 0xB0, 0xD0));
                    IsDarkTheme = false;
                    ThemeDisplayShortName = "🌊 Стекло (Свет)";
                    ThemeDisplayName = "Жидкое стекло (Светлое)";
                    break;

                case IslandTheme.Dark:
                case IslandTheme.LiquidGlassDark:
                default:
                    // Основной фон острова: #0D0D0F (почти черный с легким холодным подтоном, Opacity ~ 0.94)
                    CapsuleBackgroundBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0x0D, 0x0D, 0x0F));
                    CapsuleBorderBrush = rimLight; // 1px Rim Light
                    CapsuleBorderThickness = new Thickness(1.0);
                    // Основной текст: #F4F4F5, Вторичный текст: #A1A1AA, Карточки: #18181B, Контур: #27272A
                    ThemeForegroundBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5));
                    ThemeSecondaryForegroundBrush = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA));
                    ThemeControlBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B));
                    ThemeSubContainerBrush = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B));
                    ThemeBorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2A));
                    IsDarkTheme = true;
                    ThemeDisplayShortName = "🌑 Тёмная";
                    ThemeDisplayName = "Премиум Тёмная (#0D0D0F)";
                    break;
            }

            if (save)
            {
                _themeService.SaveSettings(new IslandSettings
                {
                    Theme = CurrentTheme,
                    EnableLiquidWaves = EnableLiquidWaves
                });
            }
        }

        [RelayCommand]
        public void CycleTheme()
        {
            var nextTheme = CurrentTheme switch
            {
                IslandTheme.LiquidGlassDark => IslandTheme.LiquidGlassLight,
                IslandTheme.LiquidGlassLight => IslandTheme.Dark,
                IslandTheme.Dark => IslandTheme.Light,
                IslandTheme.Light => IslandTheme.LiquidGlassDark,
                _ => IslandTheme.LiquidGlassDark
            };
            ApplyTheme(nextTheme, EnableLiquidWaves);
        }

        [RelayCommand]
        public void SetTheme(string themeName)
        {
            var theme = themeName switch
            {
                "Dark" => IslandTheme.Dark,
                "Light" => IslandTheme.Light,
                "GlassLight" => IslandTheme.LiquidGlassLight,
                "GlassDark" or _ => IslandTheme.LiquidGlassDark
            };
            ApplyTheme(theme, EnableLiquidWaves);
        }

        [RelayCommand]
        public void ToggleLiquidWaves()
        {
            ApplyTheme(CurrentTheme, !EnableLiquidWaves);
        }

        [RelayCommand]
        public void ToggleThemePicker()
        {
            IsThemePickerOpen = !IsThemePickerOpen;
            if (IsThemePickerOpen)
            {
                IsCityPickerOpen = false;
                IsAiSettingsOpen = false;
            }
        }

        [RelayCommand]
        public void CloseThemePicker()
        {
            IsThemePickerOpen = false;
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
            _weatherService.WeatherUpdated -= OnWeatherUpdated;
            _dropZoneService.FileAdded -= OnFileAddedToDropZone;

            _cursorTrackerService.Dispose();
            _audioVisualizerService.Dispose();
            _hardwareMonitorService.Dispose();
            _mediaService.Dispose();
            _notificationService.Dispose();
            _clipboardService.Dispose();
            _timerService.Dispose();
            _weatherService.Dispose();
            _voiceService.Dispose();
            _hudDismissTimer.Stop();
            _playbackTimer.Stop();
        }
    }
}
