using System;

namespace DynamicIsland.Models
{
    public class ChatMessageItem
    {
        public string Role { get; set; } = "user"; // "user", "assistant", "system", "tool"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsExecuting { get; set; }
        public string ActionBadge { get; set; } = string.Empty;

        public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
        public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);
        public bool HasBadge => !string.IsNullOrEmpty(ActionBadge);
    }

    public class AgentConfig
    {
        public string ProviderName { get; set; } = "OpenAI";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o-mini";
        public string SystemPrompt { get; set; } = "Ты умный персональный AI-ассистент в Dynamic Island для Windows. Ты умеешь отвечать на вопросы, помогать пользователю и при необходимости выполнять действия на компьютере через инструменты.";
        public bool EnableComputerControl { get; set; } = true;
        public bool EnableVoiceTts { get; set; } = true;
    }
}
