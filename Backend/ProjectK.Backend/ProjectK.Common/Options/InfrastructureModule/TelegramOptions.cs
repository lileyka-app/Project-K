namespace ProjectK.Common.Options.InfrastructureModule;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public TelegramDevAlertOptions DevAlerts { get; set; } = new();
}
