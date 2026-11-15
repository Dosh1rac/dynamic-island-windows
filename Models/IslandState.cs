namespace DynamicIsland.Models
{
    public enum IslandState
    {
        Minimized,      // Скрытый в виде тонкой полоски ~70x5 px
        Idle,           // Компактный 180x36 px: глазки, статус
        MediaOrGaming,  // Средний 280x44 px: мини-обложка + волны эквалайзера или CPU/GPU
        Expanded        // Раскрытый 400x180 px: полный плеер + системный монитор
    }

    public enum DisplayMode
    {
        Auto,    // Автоматическое переключение (Игра -> Музыка -> Глазки)
        Eyes,    // Принудительно интерактивные глазки
        Media,   // Принудительно медиаплеер
        Gaming   // Принудительно игровой/системный оверлей
    }
}
