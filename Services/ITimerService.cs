using System;
using System.Windows.Threading;

namespace DynamicIsland.Services
{
    public interface ITimerService : IDisposable
    {
        event EventHandler? TimerTicked;
        event EventHandler? TimerFinished;

        TimeSpan TotalDuration { get; }
        TimeSpan RemainingTime { get; }
        double Progress { get; } // 0.0 .. 1.0
        string FormattedTime { get; }
        bool IsRunning { get; }
        bool IsActive { get; }

        void Start(TimeSpan duration);
        void Pause();
        void Resume();
        void Reset();
        void AddMinutes(int minutes);
    }

    public class TimerService : ITimerService
    {
        private readonly DispatcherTimer _timer;
        private TimeSpan _totalDuration;
        private TimeSpan _remainingTime;
        private bool _isRunning;
        private bool _isActive;

        public event EventHandler? TimerTicked;
        public event EventHandler? TimerFinished;

        public TimeSpan TotalDuration => _totalDuration;
        public TimeSpan RemainingTime => _remainingTime;
        public double Progress => _totalDuration.TotalSeconds > 0 ? 1.0 - (_remainingTime.TotalSeconds / _totalDuration.TotalSeconds) : 0.0;
        public string FormattedTime => $"{_remainingTime.Minutes:D2}:{_remainingTime.Seconds:D2}";
        public bool IsRunning => _isRunning;
        public bool IsActive => _isActive;

        public TimerService()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTick;
        }

        public void Start(TimeSpan duration)
        {
            _totalDuration = duration;
            _remainingTime = duration;
            _isActive = true;
            _isRunning = true;
            _timer.Start();
            TimerTicked?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            if (_isRunning)
            {
                _isRunning = false;
                _timer.Stop();
                TimerTicked?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Resume()
        {
            if (_isActive && !_isRunning)
            {
                _isRunning = true;
                _timer.Start();
                TimerTicked?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Reset()
        {
            _isRunning = false;
            _isActive = false;
            _timer.Stop();
            _remainingTime = TimeSpan.Zero;
            _totalDuration = TimeSpan.Zero;
            TimerTicked?.Invoke(this, EventArgs.Empty);
        }

        public void AddMinutes(int minutes)
        {
            if (_isActive)
            {
                _totalDuration = _totalDuration.Add(TimeSpan.FromMinutes(minutes));
                _remainingTime = _remainingTime.Add(TimeSpan.FromMinutes(minutes));
                TimerTicked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Start(TimeSpan.FromMinutes(minutes));
            }
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_remainingTime.TotalSeconds > 1)
            {
                _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
                TimerTicked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _remainingTime = TimeSpan.Zero;
                _isRunning = false;
                _isActive = false;
                _timer.Stop();
                TimerTicked?.Invoke(this, EventArgs.Empty);
                TimerFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
