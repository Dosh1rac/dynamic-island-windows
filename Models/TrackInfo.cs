using System.Windows.Media.Imaging;

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
    }
}
