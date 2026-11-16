using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DynamicIsland.Services;
using DynamicIsland.ViewModels;
using DynamicIsland.Views;

namespace DynamicIsland
{
    public partial class App : System.Windows.Application
    {
        public static void Log(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lifecycle.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch
            {
            }
        }

        public App()
        {
            Log("App constructor called.");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log($"[DispatcherUnhandledException] {e.Exception}");
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log($"[AppDomain.UnhandledException] {e.ExceptionObject}");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Log("App OnStartup enter.");

            try
            {
                var cursorTracker = new CursorTrackerService();
                var mediaService = new MediaService();
                var audioVisualizer = new AudioVisualizerService();
                var hardwareMonitor = new HardwareMonitorService();
                var notificationService = new SystemNotificationService();
                var clipboardService = new ClipboardService();
                var timerService = new TimerService();
                var soundService = new SoundEffectService();
                var dropZoneService = new DropZoneService();
                var weatherService = new WeatherService();
                var trayService = new TrayService();
                var computerControl = new ComputerControlService();
                var aiAgentService = new AiAgentService(computerControl);
                var voiceService = new VoiceService();
                var themeService = new ThemeService();

                var viewModel = new MainViewModel(
                    cursorTracker,
                    mediaService,
                    audioVisualizer,
                    hardwareMonitor,
                    notificationService,
                    clipboardService,
                    timerService,
                    soundService,
                    dropZoneService,
                    weatherService,
                    aiAgentService,
                    voiceService,
                    themeService
                );

                var mainWindow = new MainWindow(
                    viewModel,
                    clipboardService,
                    notificationService,
                    trayService
                );

                MainWindow = mainWindow;
                mainWindow.Show();
                Log("MainWindow.Show() started successfully.");
            }
            catch (Exception ex)
            {
                Log($"[OnStartup Exception] {ex}");
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log($"App OnExit with code {e.ApplicationExitCode}.");
            base.OnExit(e);
        }
    }
}
