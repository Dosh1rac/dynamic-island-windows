using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DynamicIsland.Services
{
    public interface IWeatherService : IDisposable
    {
        string CurrentTemperature { get; }
        string WeatherDescription { get; }
        string WeatherIconPathData { get; }
        string CurrentCity { get; }
        string CurrentCountry { get; }
        bool IsAutoLocation { get; }

        event EventHandler? WeatherUpdated;

        void Start();
        Task RefreshWeatherAsync();
        Task<bool> SetManualCityAsync(string cityName);
        Task SetAutoLocationAsync();
    }

    public class WeatherSettings
    {
        public bool IsAutoLocation { get; set; } = true;
        public string City { get; set; } = "Москва";
        public string Country { get; set; } = "Россия";
        public double Latitude { get; set; } = 55.7522;
        public double Longitude { get; set; } = 37.6156;
    }

    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
        private readonly DispatcherTimer _timer;
        private readonly string _settingsFilePath;
        private WeatherSettings _settings;

        public string CurrentTemperature { get; private set; } = "--°";
        public string WeatherDescription { get; private set; } = "Обновление...";
        public string WeatherIconPathData { get; private set; } = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
        public string CurrentCity => _settings.City;
        public string CurrentCountry => _settings.Country;
        public bool IsAutoLocation => _settings.IsAutoLocation;

        public event EventHandler? WeatherUpdated;

        public WeatherService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "DynamicIsland");
            _settingsFilePath = Path.Combine(folder, "weather_settings.json");

            _settings = LoadSettings();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _timer.Tick += async (s, e) => await RefreshWeatherAsync();
        }

        public void Start()
        {
            _ = RefreshWeatherAsync();
            _timer.Start();
        }

        public async Task RefreshWeatherAsync()
        {
            try
            {
                // Если включен режим автоопределения — сначала опрашиваем гео-IP
                if (_settings.IsAutoLocation)
                {
                    await TryDetectLocationByIpAsync();
                }

                // Запрос прогноза Open-Meteo по координатам
                string latStr = _settings.Latitude.ToString(CultureInfo.InvariantCulture);
                string lonStr = _settings.Longitude.ToString(CultureInfo.InvariantCulture);
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,weather_code";

                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("current", out var current))
                {
                    double temp = current.GetProperty("temperature_2m").GetDouble();
                    int code = current.GetProperty("weather_code").GetInt32();

                    CurrentTemperature = $"{(temp > 0 ? "+" : "")}{Math.Round(temp)}°";
                    (WeatherDescription, WeatherIconPathData) = DecodeWeatherCode(code);
                    WeatherUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                App.Log($"WeatherService error: {ex.Message}");
                if (CurrentTemperature == "--°")
                {
                    CurrentTemperature = "+12°";
                    WeatherDescription = "Ясно";
                    WeatherIconPathData = "M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zM2 13h2c.55 0 1-.45 1-1s-.45-1-1-1H2c-.55 0-1 .45-1 1s.45 1 1 1zm18 0h2c.55 0 1-.45 1-1s-.45-1-1-1h-2c-.55 0-1 .45-1 1s.45 1 1 1zM11 2v2c0 .55.45 1 1 1s1-.45 1-1V2c0-.55-.45-1-1-1s-1 .45-1 1zm0 18v2c0 .55.45 1 1 1s1-.45 1-1v-2c0-.55-.45-1-1-1s-1 .45-1 1z";
                    WeatherUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public async Task<bool> SetManualCityAsync(string cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName)) return false;

            try
            {
                // Геокодинг через Open-Meteo Geocoding API (без ключей, поддерживает русский язык)
                string query = Uri.EscapeDataString(cityName.Trim());
                string url = $"https://geocoding-api.open-meteo.com/v1/search?name={query}&count=1&language=ru";

                var json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                {
                    var item = results[0];
                    string name = item.GetProperty("name").GetString() ?? cityName;
                    string country = item.TryGetProperty("country", out var c) ? (c.GetString() ?? "") : "";
                    double lat = item.GetProperty("latitude").GetDouble();
                    double lon = item.GetProperty("longitude").GetDouble();

                    _settings.IsAutoLocation = false;
                    _settings.City = name;
                    _settings.Country = country;
                    _settings.Latitude = lat;
                    _settings.Longitude = lon;

                    SaveSettings();
                    await RefreshWeatherAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                App.Log($"SetManualCityAsync error for '{cityName}': {ex.Message}");
            }

            return false;
        }

        public async Task SetAutoLocationAsync()
        {
            _settings.IsAutoLocation = true;
            SaveSettings();
            await RefreshWeatherAsync();
        }

        private async Task TryDetectLocationByIpAsync()
        {
            try
            {
                // Быстрый запрос к ip-api.com
                string url = "http://ip-api.com/json/?fields=status,city,country,lat,lon";
                var json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    string city = root.GetProperty("city").GetString() ?? "Москва";
                    string country = root.GetProperty("country").GetString() ?? "Россия";
                    double lat = root.GetProperty("lat").GetDouble();
                    double lon = root.GetProperty("lon").GetDouble();

                    _settings.City = city;
                    _settings.Country = country;
                    _settings.Latitude = lat;
                    _settings.Longitude = lon;
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                App.Log($"TryDetectLocationByIpAsync fallback: {ex.Message}");
            }
        }

        private WeatherSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<WeatherSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch (Exception ex)
            {
                App.Log($"LoadSettings error: {ex.Message}");
            }

            return new WeatherSettings();
        }

        private void SaveSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(_settingsFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                App.Log($"SaveSettings error: {ex.Message}");
            }
        }

        private static (string desc, string icon) DecodeWeatherCode(int code)
        {
            return code switch
            {
                0 => ("Ясно", "M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zM2 13h2c.55 0 1-.45 1-1s-.45-1-1-1H2c-.55 0-1 .45-1 1s.45 1 1 1zm18 0h2c.55 0 1-.45 1-1s-.45-1-1-1h-2c-.55 0-1 .45-1 1s.45 1 1 1zM11 2v2c0 .55.45 1 1 1s1-.45 1-1V2c0-.55-.45-1-1-1s-1 .45-1 1zm0 18v2c0 .55.45 1 1 1s1-.45 1-1v-2c0-.55-.45-1-1-1s-1 .45-1 1z"),
                1 or 2 or 3 => ("Облачно", "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96z"),
                61 or 63 or 65 or 80 or 81 => ("Дождь", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9v-2h2v2zm0-4H9V7h2v5z"),
                71 or 73 or 75 => ("Снег", "M19 12h-2.1l1.45-1.45-1.42-1.42L14.05 12H13V9.95l2.87-2.88-1.42-1.42L13 7.1V5h-2v2.1L9.55 5.65 8.13 7.07 11 9.95V12H9.95L7.07 9.13 5.65 10.55 7.1 12H5v2h2.1l-1.45 1.45 1.42 1.42L9.95 14H11v2.05l-2.87 2.88 1.42 1.42L11 18.9V21h2v-2.1l1.45 1.45 1.42-1.42L13 16.05V14h1.05l2.88 2.87 1.42-1.42L16.9 14H19v-2z"),
                _ => ("Переменная облачность", "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96z")
            };
        }

        public void Dispose()
        {
            _timer.Stop();
            _httpClient.Dispose();
        }
    }
}
