using System;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public interface IHardwareMonitorService : IDisposable
    {
        event EventHandler<HardwareMetrics>? MetricsUpdated;
        HardwareMetrics CurrentMetrics { get; }

        void Start();
        void Stop();
    }
}
