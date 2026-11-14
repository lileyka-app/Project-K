using FluentAssertions;
using ProjectK.Infrastructure.Logging.TelegramDevAlerts;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace ProjectK.API.Tests.Services;

/// <summary>
/// The alerts the developers' chat gets: readable at a glance, only the fields that carry something,
/// and a scanner's forty probes folded into two messages.
/// </summary>
public class TelegramAlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 16, 27, 0, TimeSpan.Zero);

    private sealed class Capture : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    /// <summary>A security event shaped exactly as <c>ActivityLogger.LogSuspicious</c> writes it.</summary>
    private static LogEvent Security(string path, string ip = "93.123.x.x", string? userAgent = "Mozilla/5.0 zgrab/0.x")
    {
        var capture = new Capture();
        using var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(capture).CreateLogger();
        using (LogContext.PushProperty("EventType", "Security.Suspicious"))
        using (LogContext.PushProperty("Action", "RateLimit.Rejected"))
        {
            logger.Warning("Security event: {Action}. {@Payload}", "RateLimit.Rejected", new
            {
                Action = "RateLimit.Rejected",
                ActorUserId = (string?)null,
                Email = (string?)null,
                Ip = ip,
                OldIp = (string?)null,
                UserAgent = userAgent,
                Path = path,
                Method = "GET",
                TraceId = "51c2dc1fd90bf5a1337bb027bc0ac54c",
                PolicyName = "global",
                CountryCode = "BG",
                Count = (int?)null,
                Reason = "Rate limit policy 'global' rejected request."
            });
        }
        return capture.Events.Single();
    }

    private static TelegramAlert Describe(LogEvent logEvent) => TelegramAlertRules.Describe(logEvent, "Production", "1.1.1", null);

    [Fact]
    public void ASecurityEvent_ReadsAsWords_WithOnlyTheFieldsThatCarrySomething()
    {
        var text = Describe(Security("/project/.git/config")).Text;

        text.Should().Contain("PRODUCTION · Ліміт запитів перевищено");
        text.Should().Contain("<code>GET /project/.git/config</code>");
        text.Should().Contain("<b>IP:</b> 93.123.x.x · BG");
        text.Should().Contain("<code>Mozilla/5.0 zgrab/0.x</code>");
        text.Should().Contain("<b>Хто:</b> анонім");
        text.Should().Contain("<b>Ліміт:</b> global");
        text.Should().Contain("1.1.1 · trace 51c2dc1fd90bf5a1337bb027bc0ac54c");
        text.Should().NotContain("null").And.NotContain("OldIp").And.NotContain("Спроб");
    }

    [Theory]
    [InlineData("/project/.git/config", true)]
    [InlineData("/old/.git-credentials", true)]
    [InlineData("/.env", true)]
    [InlineData("/wp-login.php", true)]
    [InlineData("/api/agenda/board", false)]
    public void APathNoPartOfTheAppServes_IsMarkedAsAScanner(string path, bool probe)
    {
        Describe(Security(path)).Text.Contains("Схоже на сканер").Should().Be(probe);
    }

    [Fact]
    public void WhateverCameFromTheRequest_IsEscaped()
    {
        Describe(Security("/<script>alert(1)</script>")).Text.Should().Contain("&lt;script&gt;").And.NotContain("<script>");
    }

    [Fact]
    public void AScannersBurst_IsTheFirstMessage_AndOneDigestOfTheRest()
    {
        var grouper = new TelegramAlertGrouper(TimeSpan.FromMinutes(5));
        var paths = Enumerable.Range(0, 40).Select(i => $"/probe{i:00}/.git/config").ToList();

        var sent = paths.Select((path, i) => grouper.Offer(Describe(Security(path)), Now.AddSeconds(i))).Where(t => t is not null).ToList();
        sent.Should().ContainSingle();

        grouper.Due(Now.AddMinutes(1), "Production").Should().BeEmpty("the window is still open");
        var digest = grouper.Due(Now.AddMinutes(5), "Production").Should().ContainSingle().Subject;
        digest.Should().Contain("Ліміт запитів перевищено · ще 39 за 5 хв");
        digest.Should().Contain("93.123.x.x · BG");
        digest.Should().Contain("Шляхи</b> (40, усі — пошук службових файлів)");
        digest.Should().Contain("… і ще 32");
    }

    [Fact]
    public void AnotherIp_IsItsOwnAlert()
    {
        var grouper = new TelegramAlertGrouper(TimeSpan.FromMinutes(5));

        grouper.Offer(Describe(Security("/.env", ip: "1.2.x.x")), Now).Should().NotBeNull();
        grouper.Offer(Describe(Security("/.env", ip: "5.6.x.x")), Now).Should().NotBeNull();
    }

    [Fact]
    public void ALoneEvent_LeavesNoDigestBehind()
    {
        var grouper = new TelegramAlertGrouper(TimeSpan.FromMinutes(5));
        grouper.Offer(Describe(Security("/.env")), Now);

        grouper.Due(Now.AddMinutes(10), "Production").Should().BeEmpty();
    }

    [Fact]
    public void AnError_NamesItsTypeTheRequestAndWhereInOurCodeItBroke_AndItsRequestLogIsNotSentTwice()
    {
        var capture = new Capture();
        using (var logger = new LoggerConfiguration().WriteTo.Sink(capture).CreateLogger())
        {
            Exception thrown;
            try
            {
                throw new InvalidOperationException("Sequence contains no elements");
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            logger
                .ForContext("RequestPath", "/api/agenda/board")
                .ForContext("RequestMethod", "GET")
                .ForContext("TraceId", "abc123")
                .ForContext("SourceContext", "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware")
                .Error(thrown, "An unhandled exception has occurred while executing the request.");
            logger
                .ForContext("TraceId", "abc123")
                .Error("HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms", "GET", "/api/agenda/board", 500, 12.3);
        }

        var error = Describe(capture.Events[0]);
        error.Text.Should().Contain("🔴").And.Contain("Помилка: InvalidOperationException");
        error.Text.Should().Contain("Sequence contains no elements");
        error.Text.Should().Contain("<code>GET /api/agenda/board</code>");
        error.Text.Should().Contain("<b>Де:</b>").And.Contain("ProjectK.API.Tests.Services.TelegramAlertTests");
        error.Text.Should().Contain("<code>ExceptionHandlerMiddleware</code>");

        var grouper = new TelegramAlertGrouper(TimeSpan.FromMinutes(5));
        grouper.Offer(error, Now).Should().NotBeNull();
        grouper.Offer(Describe(capture.Events[1]), Now.AddMilliseconds(5)).Should().BeNull("the 500 of the same trace is the same failure");
    }

    [Fact]
    public void EmailsAndSecrets_NeverLeave()
    {
        RedactionRules.Apply("user oksana@example.com sent token=abc123 and Bearer eyJhbGci.x.y")
            .Should().Be("user [email] sent token=[redacted] and Bearer [redacted]");
    }
}
