using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public interface IThemeService
    {
        IslandSettings Settings { get; }
        void SaveSettings(IslandSettings settings);
        IslandSettings LoadSettings();
    }

    public class ThemeService : IThemeService
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland");
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "island_settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public IslandSettings Settings { get; private set; }

        public ThemeService()
        {
            Settings = LoadSettings();
        }

        public IslandSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var loaded = JsonSerializer.Deserialize<IslandSettings>(json, JsonOptions);
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception ex)
            {
                App.Log($"Error loading island_settings: {ex.Message}");
            }
            return new IslandSettings();
        }

        public void SaveSettings(IslandSettings settings)
        {
            try
            {
                Settings = settings;
                Directory.CreateDirectory(SettingsFolder);
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                App.Log($"Error saving island_settings: {ex.Message}");
            }
        }
    }
}
