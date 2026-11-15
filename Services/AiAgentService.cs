using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public interface IAiAgentService
    {
        AgentConfig Config { get; }
        void SaveConfig(AgentConfig config);
        Task<AgentResponseResult> SendMessageAsync(List<ChatMessageItem> history, string userMessage, Action<string>? onActionExecuting = null);
    }

    public class AgentResponseResult
    {
        public string ReplyText { get; set; } = string.Empty;
        public string? ExecutedAction { get; set; }
        public string? ActionResult { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }

    public class AiAgentService : IAiAgentService
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(35) };
        private readonly IComputerControlService _computerControlService;
        private readonly string _settingsFilePath;
        private AgentConfig _config;

        public AgentConfig Config => _config;

        public AiAgentService(IComputerControlService computerControlService)
        {
            _computerControlService = computerControlService;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsFilePath = Path.Combine(appData, "DynamicIsland", "agent_settings.json");
            _config = LoadConfig();
        }

        public void SaveConfig(AgentConfig config)
        {
            _config = config;
            try
            {
                string dir = Path.GetDirectoryName(_settingsFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                App.Log($"SaveConfig error: {ex.Message}");
            }
        }

        private AgentConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var cfg = JsonSerializer.Deserialize<AgentConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                App.Log($"LoadConfig error: {ex.Message}");
            }

            return new AgentConfig();
        }

        public async Task<AgentResponseResult> SendMessageAsync(
            List<ChatMessageItem> history,
            string userMessage,
            Action<string>? onActionExecuting = null)
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                return new AgentResponseResult
                {
                    Success = false,
                    ErrorMessage = "API ключ не настроен. Нажмите ⚙ (Шестеренка вверху вкладки AI) и введите ваш ключ OpenAI, DeepSeek, Token Club или Google Gemini."
                };
            }

            try
            {
                string systemPrompt = _config.SystemPrompt;
                if (_config.EnableComputerControl)
                {
                    systemPrompt += "\n\nТы имеешь возможность выполнять команды на ПК пользователя. Если запрос пользователя требует действия, ответь ТОЛЬКО специальной командой в формате:\n" +
                                    "[ACTION: имя_действия | аргумент]\n" +
                                    "Доступные действия:\n" +
                                    "- [ACTION: open_app | имя_приложения_или_ссылка] (пример: [ACTION: open_app | telegram] или [ACTION: open_app | https://youtube.com])\n" +
                                    "- [ACTION: run_command | команда_powershell] (выполнение команды на компьютере, пример: [ACTION: run_command | Get-Process])\n" +
                                    "- [ACTION: take_screenshot | none] (сделать скриншот экрана)\n" +
                                    "- [ACTION: set_volume | процент_число] (пример: [ACTION: set_volume | 50])\n" +
                                    "- [ACTION: create_file | путь_или_имя ||| содержимое] (создать текстовый файл на ПК)\n" +
                                    "- [ACTION: read_file | путь] (прочитать текстовый файл)\n" +
                                    "- [ACTION: lock_pc | none] (заблокировать экран ПК)\n" +
                                    "Если действие не требуется — отвечай на русском языке дружелюбно, кратко и по делу.";
                }

                var messagesPayload = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                // Добавляем последние сообщения из истории (до 8 штук для экономии токенов)
                int startIdx = Math.Max(0, history.Count - 8);
                for (int i = startIdx; i < history.Count; i++)
                {
                    var msg = history[i];
                    messagesPayload.Add(new { role = msg.Role, content = msg.Content });
                }

                messagesPayload.Add(new { role = "user", content = userMessage });

                string endpoint = _config.BaseUrl.TrimEnd('/');
                if (!endpoint.EndsWith("/chat/completions"))
                {
                    endpoint += "/chat/completions";
                }

                string firstReply = await CallChatApiAsync(endpoint, _config.ApiKey, _config.Model, messagesPayload);

                // Проверяем, содержит ли ответ действие [ACTION: name | arg]
                if (_config.EnableComputerControl && TryParseAction(firstReply, out string actionName, out string actionArg))
                {
                    onActionExecuting?.Invoke($"Выполняю: {actionName}...");

                    string actionOutput = await _computerControlService.ExecuteActionAsync(actionName, actionArg);

                    // Делаем второй запрос с результатом действия, чтобы агент красиво подвел итог
                    messagesPayload.Add(new { role = "assistant", content = firstReply });
                    messagesPayload.Add(new { role = "user", content = $"Результат выполнения действия [{actionName}]: {actionOutput}. Ответь пользователю кратко о результате." });

                    string finalReply = await CallChatApiAsync(endpoint, _config.ApiKey, _config.Model, messagesPayload);

                    return new AgentResponseResult
                    {
                        Success = true,
                        ReplyText = finalReply,
                        ExecutedAction = actionName,
                        ActionResult = actionOutput
                    };
                }

                return new AgentResponseResult
                {
                    Success = true,
                    ReplyText = firstReply
                };
            }
            catch (Exception ex)
            {
                App.Log($"AiAgentService exception: {ex.Message}");
                return new AgentResponseResult
                {
                    Success = false,
                    ErrorMessage = $"Ошибка связи с AI: {ex.Message}"
                };
            }
        }

        private async Task<string> CallChatApiAsync(string endpoint, string apiKey, string model, List<object> messages)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            var bodyObj = new
            {
                model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model.Trim(),
                messages = messages,
                temperature = 0.7,
                max_tokens = 900
            };

            string bodyJson = JsonSerializer.Serialize(bodyObj);
            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);
            string respContent = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Код {resp.StatusCode}: {respContent}");
            }

            using var doc = JsonDocument.Parse(respContent);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
                {
                    return content.GetString()?.Trim() ?? string.Empty;
                }
            }

            return respContent;
        }

        private static bool TryParseAction(string text, out string actionName, out string actionArg)
        {
            actionName = string.Empty;
            actionArg = string.Empty;

            if (string.IsNullOrWhiteSpace(text)) return false;

            int actionIdx = text.IndexOf("[ACTION:", StringComparison.OrdinalIgnoreCase);
            if (actionIdx < 0) return false;

            int endIdx = text.IndexOf("]", actionIdx);
            if (endIdx < 0) return false;

            string block = text.Substring(actionIdx + 8, endIdx - (actionIdx + 8)).Trim();
            int pipeIdx = block.IndexOf('|');
            if (pipeIdx > 0)
            {
                actionName = block.Substring(0, pipeIdx).Trim();
                actionArg = block.Substring(pipeIdx + 1).Trim();
            }
            else
            {
                actionName = block;
                actionArg = "none";
            }

            return !string.IsNullOrEmpty(actionName);
        }
    }
}
