using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace DynamicIsland.Models
{
    public class TrackInfo
    {
        public string Title { get; set; } = "Нет воспроизведения";
        public string Artist { get; set; } = "Ожидание аудио...";
        public string Album { get; set; } = string.Empty;
        public BitmapImage? Thumbnail { get; set; }
        public bool IsPlaying { get; set; }
        public bool HasMedia { get; set; }
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public Color AccentColor { get; set; } = Color.FromArgb(0x35, 0x0A, 0x84, 0xFF);
    }
}
