namespace DynamicIsland.Models
{
    public class HardwareMetrics
    {
        public float CpuUsage { get; set; }
        public float CpuTemp { get; set; }
        public float GpuUsage { get; set; }
        public float GpuTemp { get; set; }
        public bool IsFullscreenGameActive { get; set; }
        public string ActiveWindowName { get; set; } = string.Empty;
    }
}
