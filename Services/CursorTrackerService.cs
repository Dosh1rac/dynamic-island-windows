using System;
using System.Windows.Threading;
using DynamicIsland.Helpers;

namespace DynamicIsland.Services
{
    public class CursorTrackerService : ICursorTrackerService
    {
        private readonly DispatcherTimer _timer;
        private readonly Random _random = new();

        private double _islandCenterX = 960;
        private double _islandCenterY = 26;

        // Текущее сглаженное положение зрачков
        private double _currentLeftX;
        private double _currentLeftY;
        private double _currentRightX;
        private double _currentRightY;

        // Моргание
        private DateTime _nextBlinkTime = DateTime.UtcNow.AddSeconds(3);
        private bool _isBlinking;
        private DateTime _blinkStartTime;
        private const double BlinkDurationMs = 150;

        private const double MaxPupilRadius = 4.5;
        private const double EyeDistanceHalf = 16.0; // расстояние между центрами глаз

        public event EventHandler<CursorTrackingEventArgs>? CursorMoved;

        public CursorTrackerService()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void UpdateIslandScreenPosition(double screenCenterX, double screenTopY)
        {
            _islandCenterX = screenCenterX;
            _islandCenterY = screenTopY;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (!NativeMethods.GetCursorPos(out var cursorPos))
                return;

            DateTime now = DateTime.UtcNow;

            // Обработка моргания
            double eyeScaleY = 1.0;
            if (!_isBlinking && now >= _nextBlinkTime)
            {
                _isBlinking = true;
                _blinkStartTime = now;
            }

            if (_isBlinking)
            {
                double elapsed = (now - _blinkStartTime).TotalMilliseconds;
                if (elapsed >= BlinkDurationMs)
                {
                    _isBlinking = false;
                    _nextBlinkTime = now.AddSeconds(_random.Next(3, 7));
                    eyeScaleY = 1.0;
                }
                else
                {
                    // Анимация веера (0 -> 1 -> 0)
                    double progress = elapsed / BlinkDurationMs;
                    // Парабола: при progress=0.5 веко закрыто (scale=0.1)
                    eyeScaleY = 0.1 + 0.9 * Math.Pow(2 * (progress - 0.5), 2);
                }
            }

            // Вычисляем целевое смещение левого и правого глаза
            double leftEyeCenterX = _islandCenterX - EyeDistanceHalf;
            double leftEyeCenterY = _islandCenterY;
            double rightEyeCenterX = _islandCenterX + EyeDistanceHalf;
            double rightEyeCenterY = _islandCenterY;

            var (targetLeftX, targetLeftY) = CalculatePupilOffset(cursorPos.X, cursorPos.Y, leftEyeCenterX, leftEyeCenterY);
            var (targetRightX, targetRightY) = CalculatePupilOffset(cursorPos.X, cursorPos.Y, rightEyeCenterX, rightEyeCenterY);

            // Плавный LERP для естественного следования
            const double lerpFactor = 0.25;
            _currentLeftX += (targetLeftX - _currentLeftX) * lerpFactor;
            _currentLeftY += (targetLeftY - _currentLeftY) * lerpFactor;
            _currentRightX += (targetRightX - _currentRightX) * lerpFactor;
            _currentRightY += (targetRightY - _currentRightY) * lerpFactor;

            CursorMoved?.Invoke(this, new CursorTrackingEventArgs
            {
                LeftPupilX = _currentLeftX,
                LeftPupilY = _currentLeftY,
                RightPupilX = _currentRightX,
                RightPupilY = _currentRightY,
                EyeScaleY = eyeScaleY
            });
        }

        private static (double x, double y) CalculatePupilOffset(double cursorX, double cursorY, double eyeX, double eyeY)
        {
            double dx = cursorX - eyeX;
            double dy = cursorY - eyeY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < 0.001)
                return (0, 0);

            // Нелинейный масштаб: чем дальше курсор, тем сильнее зрачок прижат к краю, но не более MaxPupilRadius
            double radiusFactor = Math.Min(1.0, distance / 350.0);
            double clampedDistance = MaxPupilRadius * radiusFactor;

            double offsetX = (dx / distance) * clampedDistance;
            double offsetY = (dy / distance) * clampedDistance;

            return (offsetX, offsetY);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
