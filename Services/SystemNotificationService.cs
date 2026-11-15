using System;
using System.Windows.Forms;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using DynamicIsland.Helpers;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public interface ISystemNotificationService : IDisposable
    {
        event EventHandler<HudNotification>? NotificationTriggered;

        void Initialize();
        void HandleDeviceChange(int wParam);
        void TriggerCustomNotification(HudNotification notification);
    }

    public class SystemNotificationService : ISystemNotificationService
    {
        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _defaultAudioDevice;
        private float _lastVolume = -1f;
        private bool _lastMuteState = false;

        private bool _lastCapsLock;
        private bool _lastNumLock;
        private PowerLineStatus _lastPowerStatus;

        private readonly DispatcherTimer _systemCheckTimer;

        public event EventHandler<HudNotification>? NotificationTriggered;

        public SystemNotificationService()
        {
            _systemCheckTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _systemCheckTimer.Tick += OnSystemCheckTick;
        }

        public void Initialize()
        {
            try
            {
                // Инициализация громкости CoreAudio
                _deviceEnumerator = new MMDeviceEnumerator();
                _defaultAudioDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (_defaultAudioDevice != null)
                {
                    _lastVolume = _defaultAudioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                    _lastMuteState = _defaultAudioDevice.AudioEndpointVolume.Mute;
                    _defaultAudioDevice.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemNotificationService] AudioEndpointVolume init warning: {ex.Message}");
            }

            // Инициализация начальных состояний клавиш
            _lastCapsLock = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 1) != 0;
            _lastNumLock = (NativeMethods.GetKeyState(NativeMethods.VK_NUMLOCK) & 1) != 0;
            _lastPowerStatus = SystemInformation.PowerStatus.PowerLineStatus;

            _systemCheckTimer.Start();
        }

        private void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            float currentVol = data.MasterVolume;
            bool isMuted = data.Muted;

            // Игнорируем запуск без изменений
            if (Math.Abs(currentVol - _lastVolume) < 0.005f && isMuted == _lastMuteState)
                return;

            _lastVolume = currentVol;
            _lastMuteState = isMuted;

            int percent = (int)Math.Round(currentVol * 100);
            string title = isMuted ? "Звук выключен" : $"Громкость: {percent}%";
            string icon = isMuted ? "M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3L3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4L9.91 6.09 12 8.18V4z"
                                  : (percent == 0 ? "M7 9v6h4l5 5V4l-5 5H7z" : "M3 9v6h4l5 5V4l-5 5H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z");

            TriggerCustomNotification(new HudNotification
            {
                Type = NotificationType.Volume,
                Title = title,
                Subtitle = isMuted ? "Без звука" : $"{percent}%",
                Progress = percent,
                ShowProgress = !isMuted,
                IconPathData = icon,
                AccentColor = isMuted ? "#FF453A" : "#0A84FF",
                DurationMs = 1700
            });
        }

        private void OnSystemCheckTick(object? sender, EventArgs e)
        {
            // Проверка CapsLock
            bool currentCaps = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 1) != 0;
            if (currentCaps != _lastCapsLock)
            {
                _lastCapsLock = currentCaps;
                TriggerCustomNotification(new HudNotification
                {
                    Type = NotificationType.CapsLock,
                    Title = currentCaps ? "Caps Lock Включен" : "Caps Lock Выключен",
                    Subtitle = currentCaps ? "ABC" : "abc",
                    ShowProgress = false,
                    IconPathData = "M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z",
                    AccentColor = currentCaps ? "#30D158" : "#8E8E93",
                    DurationMs = 1500
                });
            }

            // Проверка NumLock
            bool currentNum = (NativeMethods.GetKeyState(NativeMethods.VK_NUMLOCK) & 1) != 0;
            if (currentNum != _lastNumLock)
            {
                _lastNumLock = currentNum;
                TriggerCustomNotification(new HudNotification
                {
                    Type = NotificationType.NumLock,
                    Title = currentNum ? "Num Lock Включен" : "Num Lock Выключен",
                    Subtitle = currentNum ? "123" : "---",
                    ShowProgress = false,
                    IconPathData = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-4 8h-2v2h2v-2zm-4 0H9v2h2v-2zm-2-4h2v2H9V7zm4 0h2v2h-2V7z",
                    AccentColor = currentNum ? "#0A84FF" : "#8E8E93",
                    DurationMs = 1500
                });
            }

            // Проверка статуса питания (зарядка ноутбука)
            PowerLineStatus currentPower = SystemInformation.PowerStatus.PowerLineStatus;
            if (currentPower != _lastPowerStatus)
            {
                _lastPowerStatus = currentPower;
                int batteryPercent = (int)Math.Round(SystemInformation.PowerStatus.BatteryLifePercent * 100);

                if (currentPower == PowerLineStatus.Online)
                {
                    TriggerCustomNotification(new HudNotification
                    {
                        Type = NotificationType.Battery,
                        Title = "Питание подключено",
                        Subtitle = $"{batteryPercent}% заряда",
                        Progress = batteryPercent,
                        ShowProgress = true,
                        IconPathData = "M7 2v11h3v9l7-12h-4l4-8z", // молния
                        AccentColor = "#30D158",
                        DurationMs = 2000
                    });
                }
                else if (currentPower == PowerLineStatus.Offline)
                {
                    TriggerCustomNotification(new HudNotification
                    {
                        Type = NotificationType.Battery,
                        Title = "Работа от батареи",
                        Subtitle = $"{batteryPercent}% заряда",
                        Progress = batteryPercent,
                        ShowProgress = true,
                        IconPathData = "M15.67 4H14V2h-4v2H8.33C7.6 4 7 4.6 7 5.33v15.33C7 21.4 7.6 22 8.33 22h7.33c.74 0 1.34-.6 1.34-1.33V5.33C17 4.6 16.4 4 15.67 4z",
                        AccentColor = "#FF9F0A",
                        DurationMs = 2000
                    });
                }
            }
        }

        public void HandleDeviceChange(int wParam)
        {
            if (wParam == NativeMethods.DBT_DEVICEARRIVAL)
            {
                TriggerCustomNotification(new HudNotification
                {
                    Type = NotificationType.UsbConnected,
                    Title = "Устройство подключено",
                    Subtitle = "USB накопитель",
                    ShowProgress = false,
                    IconPathData = "M15 7v4h1v2h-3V5h2l-3-4-3 4h2v8H8v-2.07c.7-.37 1.2-1.08 1.2-1.93 0-1.21-.99-2.2-2.2-2.2-1.21 0-2.2.99-2.2 2.2 0 .85.5 1.56 1.2 1.93V13c0 1.11.89 2 2 2h3v3.05c-.71.37-1.2 1.1-1.2 1.95a2.2 2.2 0 104.4 0c0-.85-.49-1.58-1.2-1.95V15h3c1.11 0 2-.89 2-2v-2h1V7h-4z",
                    AccentColor = "#00E5FF",
                    DurationMs = 2200
                });
            }
            else if (wParam == NativeMethods.DBT_DEVICEREMOVECOMPLETE)
            {
                TriggerCustomNotification(new HudNotification
                {
                    Type = NotificationType.UsbDisconnected,
                    Title = "Устройство извлечено",
                    Subtitle = "USB отключен",
                    ShowProgress = false,
                    IconPathData = "M15 7v4h1v2h-3V5h2l-3-4-3 4h2v8H8v-2.07c.7-.37 1.2-1.08 1.2-1.93 0-1.21-.99-2.2-2.2-2.2-1.21 0-2.2.99-2.2 2.2 0 .85.5 1.56 1.2 1.93V13c0 1.11.89 2 2 2h3v3.05c-.71.37-1.2 1.1-1.2 1.95a2.2 2.2 0 104.4 0c0-.85-.49-1.58-1.2-1.95V15h3c1.11 0 2-.89 2-2v-2h1V7h-4z",
                    AccentColor = "#FF453A",
                    DurationMs = 2000
                });
            }
        }

        public void TriggerCustomNotification(HudNotification notification)
        {
            NotificationTriggered?.Invoke(this, notification);
        }

        public void Dispose()
        {
            _systemCheckTimer.Stop();
            if (_defaultAudioDevice != null)
            {
                try
                {
                    _defaultAudioDevice.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
                }
                catch
                {
                }
                _defaultAudioDevice.Dispose();
            }
            _deviceEnumerator?.Dispose();
        }
    }
}
