namespace ProjectK.Common.Options.NotificationsModule;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public TelegramDevAlertOptions DevAlerts { get; set; } = new();
}
