using System;
using System.Threading.Tasks;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public interface IMediaService : IDisposable
    {
        event EventHandler<TrackInfo>? MediaChanged;
        TrackInfo CurrentTrack { get; }

        Task InitializeAsync();
        Task TogglePlayPauseAsync();
        Task SkipNextAsync();
        Task SkipPreviousAsync();
    }
}
