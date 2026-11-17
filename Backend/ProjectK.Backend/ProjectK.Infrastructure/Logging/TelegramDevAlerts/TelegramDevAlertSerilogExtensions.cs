using ProjectK.Common.Options.Shared;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace ProjectK.Infrastructure.Logging.TelegramDevAlerts;

public static class TelegramDevAlertSerilogExtensions
{
    public static LoggerConfiguration TelegramDevAlerts(
        this LoggerSinkConfiguration sinkConfiguration,
        TelegramDevAlertOptions options,
        string environmentName,
        string version,
        string? codename)
    {
        return sinkConfiguration.Sink(
            new TelegramDevAlertSink(options, environmentName, version, codename),
            restrictedToMinimumLevel: ParseMinimumLevel(options.MinimumLevel));
    }

    private static LogEventLevel ParseMinimumLevel(string? value)
    {
        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Warning;
    }
}
