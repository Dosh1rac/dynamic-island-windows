using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using DynamicIsland.Helpers;
using DynamicIsland.Models;
using DynamicIsland.Services;
using DynamicIsland.ViewModels;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DynamicIsland.Views
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9001;
        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _hwndSource;
        private readonly MainViewModel _viewModel;
        private readonly IClipboardService _clipboardService;
        private readonly ISystemNotificationService _notificationService;
        private readonly ITrayService _trayService;

        // Отслеживание жестов мыши (свайпы)
        private Point _dragStartPoint;
        private bool _isDragging;
        private bool _swipeOccurred;
        private DateTime _lastMinimizedTime = DateTime.MinValue;

        // DependencyProperty для плавной интерполяции скругления углов
        public static readonly DependencyProperty CapsuleCornerRadiusProperty =
            DependencyProperty.Register(nameof(CapsuleCornerRadius), typeof(double), typeof(MainWindow),
                new PropertyMetadata(18.0));

        public double CapsuleCornerRadius
        {
            get => (double)GetValue(CapsuleCornerRadiusProperty);
            set => SetValue(CapsuleCornerRadiusProperty, value);
        }

        private readonly IEasingFunction _springEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 };
        private readonly IEasingFunction _smoothEase = new QuarticEase { EasingMode = EasingMode.EaseOut };

        public MainWindow(
            MainViewModel viewModel,
            IClipboardService clipboardService,
            ISystemNotificationService notificationService,
            ITrayService trayService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _clipboardService = clipboardService;
            _notificationService = notificationService;
            _trayService = trayService;
            DataContext = _viewModel;

            _viewModel.RequestClickThroughChanged += OnRequestClickThroughChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            App.Log("MainWindow Window_Loaded enter.");
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - Width) / 2;
            Top = 6;
            App.Log($"MainWindow positioned at Left={Left}, Top={Top}, Width={Width}, Height={Height}.");

            _hwnd = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(HwndHook);

            // Win32 стили
            NativeMethods.SetToolWindowAndTopmost(_hwnd);

            // Регистрация хоткея Ctrl + Shift + I
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, NativeMethods.VK_I);

            // Инициализация буфера обмена для перехвата WM_CLIPBOARDUPDATE
            _clipboardService.Initialize(_hwnd);

            // Инициализация иконки в системном трее
            _trayService.Initialize(
                onToggleVisible: () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_viewModel.CurrentState == IslandState.Minimized)
                        {
                            _viewModel.RestoreFromMinimized();
                        }
                        else
                        {
                            _viewModel.ToggleExpand();
                        }
                    });
                },
                onToggleClickThrough: () =>
                {
                    Dispatcher.Invoke(() => _viewModel.ToggleClickThrough());
                },
                onExit: () =>
                {
                    Dispatcher.Invoke(() => App.Current?.Shutdown());
                }
            );

            double centerX = Left + (Width / 2);
            double centerY = Top + 18;
            _viewModel.UpdateScreenPosition(centerX, centerY);

            UpdateContentVisibility(immediate: true);
            App.Log("MainWindow Window_Loaded finish.");
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentState) ||
                e.PropertyName == nameof(MainViewModel.ActiveCompactView) ||
                e.PropertyName == nameof(MainViewModel.IsShowingNotification))
            {
                Dispatcher.InvokeAsync(AnimateCapsuleState);
            }
        }

        private void AnimateCapsuleState()
        {
            var duration = TimeSpan.FromMilliseconds(340);
            var easing = _viewModel.CurrentState == IslandState.Minimized ? _smoothEase : _springEase;

            // Анимация ширины
            var widthAnim = new DoubleAnimation
            {
                To = _viewModel.IslandWidth,
                Duration = duration,
                EasingFunction = easing
            };

            // Анимация высоты
            var heightAnim = new DoubleAnimation
            {
                To = _viewModel.IslandHeight,
                Duration = duration,
                EasingFunction = easing
            };

            // Анимация скругления углов
            var cornerAnim = new DoubleAnimation
            {
                To = _viewModel.IslandCornerRadius,
                Duration = duration,
                EasingFunction = easing
            };

            IslandCapsule.BeginAnimation(WidthProperty, widthAnim);
            IslandCapsule.BeginAnimation(HeightProperty, heightAnim);
            BeginAnimation(CapsuleCornerRadiusProperty, cornerAnim);

            UpdateContentVisibility(immediate: false);
        }

        private void UpdateContentVisibility(bool immediate)
        {
            var currentState = _viewModel.CurrentState;
            var activeView = _viewModel.ActiveCompactView;
            bool isNotification = _viewModel.IsShowingNotification;

            UIElement? targetElement = null;

            if (isNotification)
            {
                targetElement = HudContent;
            }
            else if (currentState == IslandState.Minimized)
            {
                targetElement = MinimizedBarContent;
            }
            else if (currentState == IslandState.Expanded)
            {
                targetElement = ExpandedContent;
            }
            else
            {
                targetElement = activeView switch
                {
                    "Media" => CompactMediaContent,
                    "Gaming" => CompactGamingContent,
                    _ => IdleContent
                };
            }

            UIElement[] allElements = { MinimizedBarContent, HudContent, IdleContent, CompactMediaContent, CompactGamingContent, ExpandedContent };

            foreach (var elem in allElements)
            {
                if (elem == targetElement)
                {
                    elem.Visibility = Visibility.Visible;
                    if (immediate)
                    {
                        elem.Opacity = 1.0;
                    }
                    else
                    {
                        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
                        {
                            EasingFunction = _smoothEase
                        };
                        elem.BeginAnimation(OpacityProperty, fadeIn);
                    }
                }
                else
                {
                    elem.Visibility = Visibility.Collapsed;
                    elem.Opacity = 0.0;
                }
            }
        }

        // ================= СВАЙПЫ И КЛИКИ =================

        private void IslandCapsule_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = true;
            _swipeOccurred = false;
        }

        private void IslandCapsule_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPoint = e.GetPosition(this);
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            // Свайп вверх: сворачивание в полоску
            if (deltaY < -10 && _viewModel.CurrentState != IslandState.Minimized)
            {
                _swipeOccurred = true;
                _isDragging = false;
                _lastMinimizedTime = DateTime.UtcNow;
                _viewModel.MinimizeToBarCommand.Execute(null);
            }
            // Свайп вниз из полоски: разворачивание
            else if (deltaY > 10 && _viewModel.CurrentState == IslandState.Minimized)
            {
                if ((DateTime.UtcNow - _lastMinimizedTime).TotalMilliseconds > 400)
                {
                    _swipeOccurred = true;
                    _isDragging = false;
                    _viewModel.RestoreFromMinimizedCommand.Execute(null);
                }
            }
        }

        private void IslandCapsule_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool wasDragging = _isDragging;
            _isDragging = false;

            if (_swipeOccurred)
            {
                _swipeOccurred = false;
                return;
            }

            if (!wasDragging) return;

            Point endPoint = e.GetPosition(this);
            double deltaY = Math.Abs(endPoint.Y - _dragStartPoint.Y);
            double deltaX = Math.Abs(endPoint.X - _dragStartPoint.X);

            if (deltaY < 8 && deltaX < 8)
            {
                if ((DateTime.UtcNow - _lastMinimizedTime).TotalMilliseconds < 400)
                    return;

                if (e.OriginalSource is not System.Windows.Controls.Button &&
                    e.OriginalSource is not System.Windows.Shapes.Path)
                {
                    if (_viewModel.CurrentState == IslandState.Minimized)
                    {
                        _viewModel.RestoreFromMinimizedCommand.Execute(null);
                    }
                    else
                    {
                        _viewModel.ToggleExpandCommand.Execute(null);
                    }
                    e.Handled = true;
                }
            }
        }

        private void IslandCapsule_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0 && _viewModel.CurrentState != IslandState.Minimized)
            {
                _lastMinimizedTime = DateTime.UtcNow;
                _viewModel.MinimizeToBarCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Delta < 0 && _viewModel.CurrentState == IslandState.Minimized)
            {
                _viewModel.RestoreFromMinimizedCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnRequestClickThroughChanged(bool isClickThrough)
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.SetClickThrough(_hwnd, isClickThrough);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _viewModel.ToggleClickThrough();
                handled = true;
            }
            else if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                _clipboardService.HandleClipboardUpdateMessage();
            }
            else if (msg == NativeMethods.WM_DEVICECHANGE)
            {
                _notificationService.HandleDeviceChange(wParam.ToInt32());
            }
            return IntPtr.Zero;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            App.Log("MainWindow Window_Closing enter.");
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
                _hwndSource?.RemoveHook(HwndHook);
            }
            _trayService.Dispose();
            _clipboardService.Dispose();
            _notificationService.Dispose();
            _viewModel.Dispose();
            App.Log("MainWindow Window_Closing exit.");
        }
    }
}
