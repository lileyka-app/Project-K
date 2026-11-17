namespace ProjectK.Common.Options.Shared;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public TelegramDevAlertOptions DevAlerts { get; set; } = new();
}
