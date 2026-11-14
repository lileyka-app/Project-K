namespace ProjectK.Common.Options.InfrastructureModule;

public sealed class TelegramOptions
{
    public TelegramDevAlertOptions DevAlerts { get; set; } = new();
}
