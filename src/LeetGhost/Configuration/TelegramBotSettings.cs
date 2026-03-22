namespace LeetGhost.Configuration;

/// <summary>
/// Telegram Bot settings.
/// </summary>
public class TelegramBotSettings
{
    public const string SectionName = "TelegramBot";

    /// <summary>
    /// Bot token from @BotFather.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional: restrict bot to specific chat IDs.
    /// Empty list means allow all users.
    /// </summary>
    public List<long> AllowedChatIds { get; set; } = new();
}
