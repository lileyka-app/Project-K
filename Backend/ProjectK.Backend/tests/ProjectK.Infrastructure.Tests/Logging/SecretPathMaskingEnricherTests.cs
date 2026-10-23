using ProjectK.Infrastructure.Logging;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Logging;

/// <summary>The calendar link's token must not reach a log line, a Telegram alert or telemetry.</summary>
public class SecretPathMaskingEnricherTests
{
    private static LogEvent Event(string path) => new(
        DateTimeOffset.UtcNow, LogEventLevel.Error, null, new MessageTemplateParser().Parse("x"),
        [new LogEventProperty("RequestPath", new ScalarValue(path))]);

    private static string? PathOf(LogEvent logEvent) =>
        (logEvent.Properties["RequestPath"] as ScalarValue)?.Value as string;

    [Fact]
    public void TheTokenOfACalendarLink_IsCutOff()
    {
        var logEvent = Event("/api/calendar/feed/very-secret-token.ics");

        new SecretPathMaskingEnricher().Enrich(logEvent, new PropertyFactory());

        Assert.Equal("/api/calendar/feed/***", PathOf(logEvent));
    }

    [Fact]
    public void AnyOtherPath_IsLeftAlone()
    {
        var logEvent = Event("/api/me/calendar-feed");

        new SecretPathMaskingEnricher().Enrich(logEvent, new PropertyFactory());

        Assert.Equal("/api/me/calendar-feed", PathOf(logEvent));
    }

    private sealed class PropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
