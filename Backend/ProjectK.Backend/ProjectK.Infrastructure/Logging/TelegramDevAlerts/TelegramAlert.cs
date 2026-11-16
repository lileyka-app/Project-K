using Serilog.Events;

namespace ProjectK.Infrastructure.Logging.TelegramDevAlerts;

/// <summary>What one log event says, read once: the alert, its group, and what a digest of the group needs.</summary>
public sealed record TelegramAlert(
    string GroupKey,
    string Text,
    string Title,
    LogEventLevel Level,
    string? Ip,
    string? Country,
    string? Path,
    string? TraceId,
    bool IsRequestLog);
