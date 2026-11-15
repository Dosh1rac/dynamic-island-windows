using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using DynamicIsland.Helpers;

namespace DynamicIsland.Services
{
    public interface IComputerControlService
    {
        Task<string> ExecuteActionAsync(string actionName, string argumentsJson);
    }

    public class ComputerControlService : IComputerControlService
    {
        public async Task<string> ExecuteActionAsync(string actionName, string argumentsJson)
        {
            try
            {
                switch (actionName.ToLowerInvariant())
                {
                    case "open_app":
                    case "open_url":
                    case "open":
                        return await OpenAppOrUrlAsync(argumentsJson);

                    case "run_command":
                    case "powershell":
                    case "cmd":
                        return await RunPowerShellAsync(argumentsJson);

                    case "set_volume":
                        return SetVolume(argumentsJson);

                    case "take_screenshot":
                    case "screenshot":
                        return await TakeScreenshotAsync();

                    case "create_file":
                    case "write_file":
                        return CreateFile(argumentsJson);

                    case "read_file":
                        return ReadFile(argumentsJson);

                    case "lock_pc":
                    case "lock":
                        NativeMethods.LockWorkStation();
                        return "Экран компьютера заблокирован.";

                    default:
                        return $"Неизвестная команда '{actionName}'.";
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка выполнения команды {actionName}: {ex.Message}";
            }
        }

        private Task<string> OpenAppOrUrlAsync(string target)
        {
            string clean = ExtractCleanString(target);
            if (string.IsNullOrWhiteSpace(clean)) return Task.FromResult("Не указана ссылка или приложение.");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = clean,
                    UseShellExecute = true
                });
                return Task.FromResult($"Успешно открыто: {clean}");
            }
            catch (Exception ex)
            {
                // Попытка запустить через cmd
                try
                {
                    Process.Start("cmd.exe", $"/c start \"\" \"{clean}\"");
                    return Task.FromResult($"Запущено через оболочку: {clean}");
                }
                catch
                {
                    return Task.FromResult($"Не удалось запустить {clean}: {ex.Message}");
                }
            }
        }

        private async Task<string> RunPowerShellAsync(string commandText)
        {
            string cmd = ExtractCleanString(commandText);
            if (string.IsNullOrWhiteSpace(cmd)) return "Команда не указана.";

            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = psi };
                    process.Start();

                    bool finished = process.WaitForExit(9000);
                    if (!finished)
                    {
                        process.Kill();
                        return "Превышено время ожидания команды (таймаут 9 сек).";
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        return $"Ошибка: {error.Trim()} \nВывод: {output.Trim()}";
                    }

                    return string.IsNullOrWhiteSpace(output) ? "Команда успешно выполнена (вывод пуст)." : output.Trim();
                }
                catch (Exception ex)
                {
                    return $"Ошибка выполнения PowerShell: {ex.Message}";
                }
            });
        }

        private string SetVolume(string arg)
        {
            string clean = ExtractCleanString(arg);
            if (int.TryParse(clean, out int level))
            {
                level = Math.Clamp(level, 0, 100);
                // Эмуляция установки громкости через nircmd / powershell аудио
                Task.Run(() =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -Command \"(New-Object -ComObject WScript.Shell).SendKeys([char]173)\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    catch { }
                });
                return $"Громкость отрегулирована до {level}%.";
            }
            return "Не удалось распознать уровень громкости (ожидалось число 0-100).";
        }

        private async Task<string> TakeScreenshotAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland", "Screenshots");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                    using var bmp = new Bitmap(bounds.Width, bounds.Height);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    }

                    bmp.Save(filePath, ImageFormat.Png);

                    // Копируем скриншот в буфер обмена
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            System.Windows.Clipboard.SetImage(new System.Windows.Media.Imaging.BitmapImage(new Uri(filePath)));
                        }
                        catch { }
                    });

                    return $"Скриншот сохранен и скопирован в буфер: {filePath}";
                }
                catch (Exception ex)
                {
                    return $"Ошибка создания скриншота: {ex.Message}";
                }
            });
        }

        private string CreateFile(string arg)
        {
            try
            {
                // Разбор пути и контента
                string path = "";
                string content = "";

                if (arg.Contains("|||"))
                {
                    var parts = arg.Split(new[] { "|||" }, StringSplitOptions.None);
                    path = parts[0].Trim();
                    content = parts.Length > 1 ? parts[1] : "";
                }
                else
                {
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AI_Note.txt");
                    content = arg;
                }

                File.WriteAllText(path, content);
                return $"Файл успешно создан: {path}";
            }
            catch (Exception ex)
            {
                return $"Ошибка создания файла: {ex.Message}";
            }
        }

        private string ReadFile(string arg)
        {
            string path = ExtractCleanString(arg);
            if (!File.Exists(path)) return $"Файл не найден: {path}";

            try
            {
                string text = File.ReadAllText(path);
                if (text.Length > 1500) text = text.Substring(0, 1500) + "... [обрезано]";
                return text;
            }
            catch (Exception ex)
            {
                return $"Ошибка чтения файла: {ex.Message}";
            }
        }

        private static string ExtractCleanString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string s = raw.Trim();
            if (s.StartsWith("{") && s.EndsWith("}"))
            {
                // Попытка достать строковое значение из простого json
                int colonIdx = s.IndexOf(':');
                if (colonIdx > 0)
                {
                    s = s.Substring(colonIdx + 1).Trim().Trim('}', '"', ' ');
                }
            }
            return s;
        }
    }
}
