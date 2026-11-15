namespace DynamicIsland.Models
{
    public enum NotificationType
    {
        Volume,
        CapsLock,
        NumLock,
        UsbConnected,
        UsbDisconnected,
        Clipboard,
        Battery,
        TimerFinished
    }

    public class HudNotification
    {
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public double Progress { get; set; } // 0..100
        public bool ShowProgress { get; set; }
        public int DurationMs { get; set; } = 1800;
        public string IconPathData { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#FFFFFF";
    }
}
