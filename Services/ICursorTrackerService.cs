using System;

namespace DynamicIsland.Services
{
    public class CursorTrackingEventArgs : EventArgs
    {
        public double LeftPupilX { get; set; }
        public double LeftPupilY { get; set; }
        public double RightPupilX { get; set; }
        public double RightPupilY { get; set; }
        public double EyeScaleY { get; set; } = 1.0;
    }

    public interface ICursorTrackerService : IDisposable
    {
        event EventHandler<CursorTrackingEventArgs>? CursorMoved;
        void Start();
        void Stop();
        void UpdateIslandScreenPosition(double screenCenterX, double screenTopY);
    }
}
