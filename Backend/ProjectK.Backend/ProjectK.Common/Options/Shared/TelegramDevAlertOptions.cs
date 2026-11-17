namespace ProjectK.Common.Options.Shared;

public sealed class TelegramDevAlertOptions
{
    public const string SectionName = "Telegram:DevAlerts";

    public bool Enabled { get; set; }

    public string? BotToken { get; set; }

    public string? ChatId { get; set; }

    public string BaseUrl { get; set; } = "https://api.telegram.org";

    public int TimeoutSeconds { get; set; } = 10;

    public bool DisableNotification { get; set; }

    public string MinimumLevel { get; set; } = "Warning";

    public int MaxMessageLength { get; set; } = 3500;

    /// <summary>
    /// How long alike events (same action from the same IP, same error) are gathered after the first
    /// one went out; the rest arrive as one digest when it closes.
    /// </summary>
    public int GroupWindowSeconds { get; set; } = 300;
}
