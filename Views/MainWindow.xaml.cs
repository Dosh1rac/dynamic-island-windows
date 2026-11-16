using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicIsland.Helpers;
using DynamicIsland.Models;
using DynamicIsland.Services;
using DynamicIsland.ViewModels;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;

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

        // Отслеживание свайпов
        private Point _dragStartPoint;
        private bool _isDragging;
        private bool _swipeOccurred;
        private DateTime _lastMinimizedTime = DateTime.MinValue;

        public static readonly DependencyProperty CapsuleCornerRadiusProperty =
            DependencyProperty.Register(nameof(CapsuleCornerRadius), typeof(double), typeof(MainWindow),
                new PropertyMetadata(18.0));

        public double CapsuleCornerRadius
        {
            get => (double)GetValue(CapsuleCornerRadiusProperty);
            set => SetValue(CapsuleCornerRadiusProperty, value);
        }

        private readonly IEasingFunction _springEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 };
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

            NativeMethods.SetToolWindowAndTopmost(_hwnd);
            NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, NativeMethods.VK_I);

            _clipboardService.Initialize(_hwnd);

            _trayService.Initialize(
                onToggleVisible: () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_viewModel.CurrentState == IslandState.Minimized)
                            _viewModel.RestoreFromMinimized();
                        else
                            _viewModel.ToggleExpand();
                    });
                },
                onToggleClickThrough: () => Dispatcher.Invoke(() => _viewModel.ToggleClickThrough()),
                onPickCity: () => Dispatcher.Invoke(() =>
                {
                    _viewModel.ExpandIsland();
                    _viewModel.IsCityPickerOpen = true;
                }),
                onPickTheme: () => Dispatcher.Invoke(() =>
                {
                    _viewModel.ExpandIsland();
                    _viewModel.IsThemePickerOpen = true;
                }),
                onExit: () => Dispatcher.Invoke(() => App.Current?.Shutdown())
            );

            double centerX = Left + (Width / 2);
            double centerY = Top + 36;
            _viewModel.UpdateScreenPosition(centerX, centerY);

            ((INotifyCollectionChanged)_viewModel.ChatMessages).CollectionChanged += (s, ev) =>
            {
                if (ev.Action == NotifyCollectionChangedAction.Add)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AgentChatScrollViewer?.ScrollToEnd();
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };

            StartLiquidAnimations();

            UpdateContentVisibility(immediate: true);
            App.Log("MainWindow Window_Loaded finish.");
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentState) ||
                e.PropertyName == nameof(MainViewModel.ActiveCompactView) ||
                e.PropertyName == nameof(MainViewModel.IsShowingNotification) ||
                e.PropertyName == nameof(MainViewModel.ExpandedTab) ||
                e.PropertyName == nameof(MainViewModel.IslandWidth) ||
                e.PropertyName == nameof(MainViewModel.IslandHeight))
            {
                Dispatcher.InvokeAsync(AnimateCapsuleState);
            }
            else if (e.PropertyName == nameof(MainViewModel.EnableLiquidWaves))
            {
                Dispatcher.InvokeAsync(StartLiquidAnimations);
            }
        }

        // ================= ЖИДКАЯ АНИМАЦИЯ SQUASH & STRETCH =================

        private void AnimateCapsuleState()
        {
            var currentState = _viewModel.CurrentState;
            bool isNotification = _viewModel.IsShowingNotification;

            TimeSpan duration = TimeSpan.FromMilliseconds(380);
            IEasingFunction easing = currentState == IslandState.Minimized ? _smoothEase : _springEase;

            var widthAnim = new DoubleAnimation
            {
                To = _viewModel.IslandWidth,
                Duration = duration,
                EasingFunction = easing
            };

            var heightAnim = new DoubleAnimation
            {
                To = _viewModel.IslandHeight,
                Duration = duration,
                EasingFunction = easing
            };

            var cornerAnim = new DoubleAnimation
            {
                To = _viewModel.IslandCornerRadius,
                Duration = duration,
                EasingFunction = easing
            };

            IslandCapsule.BeginAnimation(WidthProperty, widthAnim);
            IslandCapsule.BeginAnimation(HeightProperty, heightAnim);
            BeginAnimation(CapsuleCornerRadiusProperty, cornerAnim);

            ApplyLiquidPhysics(currentState, isNotification);
            UpdateContentVisibility(immediate: false);
        }

        private void ApplyLiquidPhysics(IslandState state, bool isNotification)
        {
            var keySplineFast = new KeySpline(0.1, 0.9, 0.2, 1.0);
            var keySplineBounce = new KeySpline(0.3, 1.3, 0.4, 1.0);

            var scaleXAnim = new DoubleAnimationUsingKeyFrames();
            var scaleYAnim = new DoubleAnimationUsingKeyFrames();

            if (isNotification)
            {
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60)), keySplineFast));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)), keySplineBounce));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.985, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(310))));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));

                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60)), keySplineFast));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)), keySplineBounce));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.01, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(310))));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
            }
            else if (state == IslandState.Expanded)
            {
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70)), keySplineFast));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.07, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230)), keySplineBounce));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(440))));

                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70)), keySplineFast));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.97, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230)), keySplineBounce));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.015, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(440))));
            }
            else if (state == IslandState.Minimized)
            {
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)), keySplineFast));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));

                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.93, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)), keySplineFast));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.04, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
            }
            else
            {
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)), keySplineFast));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));

                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.97, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)), keySplineFast));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.01, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
            }

            CapsuleScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            CapsuleScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        }

        private void UpdateContentVisibility(bool immediate)
        {
            var currentState = _viewModel.CurrentState;
            var activeView = _viewModel.ActiveCompactView;
            bool isNotification = _viewModel.IsShowingNotification;

            UIElement? targetElement = null;
            ScaleTransform? targetScale = null;

            if (isNotification)
            {
                targetElement = HudContent;
                targetScale = HudScale;
            }
            else if (currentState == IslandState.Minimized)
            {
                targetElement = MinimizedBarContent;
            }
            else if (currentState == IslandState.Expanded)
            {
                targetElement = ExpandedContent;
                targetScale = ExpandedScale;
            }
            else
            {
                switch (activeView)
                {
                    case "Media":
                        targetElement = CompactMediaContent;
                        targetScale = MediaScale;
                        break;
                    case "Gaming":
                        targetElement = CompactGamingContent;
                        targetScale = GamingScale;
                        break;
                    default:
                        targetElement = IdleContent;
                        targetScale = IdleScale;
                        break;
                }
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
                        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240))
                        {
                            EasingFunction = _smoothEase
                        };
                        elem.BeginAnimation(OpacityProperty, fadeIn);

                        if (targetScale != null)
                        {
                            var scalePop = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(280))
                            {
                                EasingFunction = _springEase
                            };
                            targetScale.BeginAnimation(ScaleTransform.ScaleXProperty, scalePop);
                            targetScale.BeginAnimation(ScaleTransform.ScaleYProperty, scalePop);
                        }
                    }
                }
                else
                {
                    elem.Visibility = Visibility.Collapsed;
                    elem.Opacity = 0.0;
                }
            }
        }

        // ================= DROP ZONE (DRAG & DROP) =================

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                _viewModel.IsDragOverIsland = true;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            _viewModel.IsDragOverIsland = false;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _viewModel.HandleFilesDropped(files);
                    if (_viewModel.CurrentState != IslandState.Expanded)
                    {
                        _viewModel.ToggleExpand();
                    }
                }
            }
            _viewModel.IsDragOverIsland = false;
            e.Handled = true;
        }

        // ================= ЖЕСТЫ И СВАЙПЫ =================

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

            if (deltaY < -10 && _viewModel.CurrentState != IslandState.Minimized)
            {
                _swipeOccurred = true;
                _isDragging = false;
                _lastMinimizedTime = DateTime.UtcNow;
                _viewModel.MinimizeToBarCommand.Execute(null);
            }
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
                    e.OriginalSource is not System.Windows.Shapes.Path &&
                    e.OriginalSource is not System.Windows.Controls.TextBox)
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
            // В раскрытом режиме колесо мыши используется для скролла списков (чат, буфер, файлы)
            if (_viewModel.CurrentState == IslandState.Expanded)
            {
                return;
            }

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

        private void AgentChatScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scv)
            {
                double newOffset = scv.VerticalOffset - (e.Delta * 0.7);
                scv.ScrollToVerticalOffset(Math.Max(0, Math.Min(scv.ScrollableHeight, newOffset)));
                e.Handled = true;
            }
        }

        private void StartLiquidAnimations()
        {
            try
            {
                if (_viewModel.EnableLiquidWaves)
                {
                    // 1. Бегущая рябь / каустическая световая волна преломления
                    var waveAnim = new DoubleAnimation
                    {
                        From = -450,
                        To = 450,
                        Duration = TimeSpan.FromSeconds(5.2),
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    WaveTranslate.BeginAnimation(TranslateTransform.XProperty, waveAnim);

                    // 2. Мягкая плавучесть и дыхание капли (organic liquid buoyancy)
                    var buoyAnim = new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.6,
                        Duration = TimeSpan.FromSeconds(3.2),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    LiquidBuoyancyTranslate.BeginAnimation(TranslateTransform.YProperty, buoyAnim);
                }
                else
                {
                    WaveTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    LiquidBuoyancyTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    WaveTranslate.X = -450;
                    LiquidBuoyancyTranslate.Y = 0.0;
                }
            }
            catch (Exception ex)
            {
                App.Log($"StartLiquidAnimations error: {ex.Message}");
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
