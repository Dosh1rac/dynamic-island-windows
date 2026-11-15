using System;

namespace DynamicIsland.Services
{
    public interface IAudioVisualizerService : IDisposable
    {
        event EventHandler<float[]>? SpectrumUpdated;
        float[] CurrentBands { get; }
        bool IsRunning { get; }

        void Start();
        void Stop();
    }
}
