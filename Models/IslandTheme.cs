using System;

namespace DynamicIsland.Models
{
    public enum IslandTheme
    {
        Dark,             // Тёмная (OLED чёрная)
        Light,            // Светлая (Керамика белая)
        LiquidGlassDark,  // Жидкое стекло (Тёмное)
        LiquidGlassLight  // Жидкое стекло (Светлое)
    }

    public class IslandSettings
    {
        public IslandTheme Theme { get; set; } = IslandTheme.LiquidGlassDark;
        public bool EnableLiquidWaves { get; set; } = true;
    }
}
