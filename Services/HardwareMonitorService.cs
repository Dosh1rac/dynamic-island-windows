using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LibreHardwareMonitor.Hardware;
using DynamicIsland.Helpers;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public class HardwareMonitorService : IHardwareMonitorService
    {
        private readonly System.Threading.Timer _timer;
        private Computer? _computer;
        private readonly HardwareMetrics _currentMetrics = new();
        private bool _isInitialized;
        private bool _isInitializing;
        private readonly object _lock = new();

        public event EventHandler<HardwareMetrics>? MetricsUpdated;
        public HardwareMetrics CurrentMetrics => _currentMetrics;

        public HardwareMonitorService()
        {
            _timer = new System.Threading.Timer(OnTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            // Инициализация в фоновом потоке, чтобы никогда не блокировать UI
            Task.Run(() =>
            {
                InitializeHardwareMonitor();
            });

            _timer.Change(1000, 1500); // Опрос раз в 1.5 сек
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            CloseHardwareMonitor();
        }

        private void InitializeHardwareMonitor()
        {
            lock (_lock)
            {
                if (_isInitialized || _isInitializing) return;
                _isInitializing = true;
            }

            try
            {
                var computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true
                };
                computer.Open();

                lock (_lock)
                {
                    _computer = computer;
                    _isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HardwareMonitor] LibreHardwareMonitor init warning: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _isInitializing = false;
                }
            }
        }

        private void CloseHardwareMonitor()
        {
            lock (_lock)
            {
                if (!_isInitialized || _computer == null) return;

                try
                {
                    _computer.Close();
                }
                catch
                {
                }
                finally
                {
                    _computer = null;
                    _isInitialized = false;
                }
            }
        }

        private void OnTimerCallback(object? state)
        {
            lock (_lock)
            {
                float cpuUsage = 0f;
                float cpuTemp = 0f;
                float gpuUsage = 0f;
                float gpuTemp = 0f;

                if (_computer != null)
                {
                    try
                    {
                        foreach (IHardware hardware in _computer.Hardware)
                        {
                            hardware.Update();
                            foreach (IHardware subHardware in hardware.SubHardware)
                            {
                                subHardware.Update();
                            }

                            if (hardware.HardwareType == HardwareType.Cpu)
                            {
                                foreach (ISensor sensor in hardware.Sensors)
                                {
                                    if (sensor.SensorType == SensorType.Load &&
                                        (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                                         sensor.Name.Contains("CPU Total", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        if (sensor.Value.HasValue) cpuUsage = sensor.Value.Value;
                                    }
                                    else if (sensor.SensorType == SensorType.Temperature)
                                    {
                                        if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                            sensor.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase) ||
                                            sensor.Name.Contains("Core #1", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (sensor.Value.HasValue && sensor.Value.Value > cpuTemp)
                                            {
                                                cpuTemp = sensor.Value.Value;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                                     hardware.HardwareType == HardwareType.GpuAmd ||
                                     hardware.HardwareType == HardwareType.GpuIntel)
                            {
                                foreach (ISensor sensor in hardware.Sensors)
                                {
                                    if (sensor.SensorType == SensorType.Load &&
                                        (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                         sensor.Name.Contains("D3D 3D", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        if (sensor.Value.HasValue && sensor.Value.Value > gpuUsage)
                                        {
                                            gpuUsage = sensor.Value.Value;
                                        }
                                    }
                                    else if (sensor.SensorType == SensorType.Temperature &&
                                             (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                              sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        if (sensor.Value.HasValue && sensor.Value.Value > gpuTemp)
                                        {
                                            gpuTemp = sensor.Value.Value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HardwareMonitor] Ошибка чтения сенсоров: {ex.Message}");
                    }
                }

                // Проверка на полноэкранное приложение (игру)
                bool isFullscreen = DetectFullscreenGame(out string winTitle);

                _currentMetrics.CpuUsage = (float)Math.Round(cpuUsage, 1);
                _currentMetrics.CpuTemp = (float)Math.Round(cpuTemp, 1);
                _currentMetrics.GpuUsage = (float)Math.Round(gpuUsage, 1);
                _currentMetrics.GpuTemp = (float)Math.Round(gpuTemp, 1);
                _currentMetrics.IsFullscreenGameActive = isFullscreen;
                _currentMetrics.ActiveWindowName = winTitle;

                MetricsUpdated?.Invoke(this, _currentMetrics);
            }
        }

        private static bool DetectFullscreenGame(out string windowTitle)
        {
            windowTitle = string.Empty;
            try
            {
                IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();
                if (foregroundHwnd == IntPtr.Zero) return false;

                IntPtr desktopHwnd = NativeMethods.GetDesktopWindow();
                IntPtr shellHwnd = NativeMethods.GetShellWindow();
                if (foregroundHwnd == desktopHwnd || foregroundHwnd == shellHwnd)
                    return false;

                StringBuilder sb = new StringBuilder(256);
                NativeMethods.GetClassName(foregroundHwnd, sb, sb.Capacity);
                string className = sb.ToString();

                if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
                    return false;

                if (NativeMethods.GetWindowRect(foregroundHwnd, out var rect))
                {
                    int screenW = (int)SystemParameters.PrimaryScreenWidth;
                    int screenH = (int)SystemParameters.PrimaryScreenHeight;

                    bool coversScreen = rect.Left <= 0 && rect.Top <= 0 &&
                                        rect.Right >= screenW && rect.Bottom >= screenH;

                    if (coversScreen)
                    {
                        windowTitle = className;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
        }
    }
}
