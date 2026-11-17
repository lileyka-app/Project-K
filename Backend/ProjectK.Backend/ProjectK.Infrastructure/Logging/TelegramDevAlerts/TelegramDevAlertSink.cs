using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using ProjectK.Common.Options.NotificationsModule;
using Serilog.Core;
using Serilog.Events;

namespace ProjectK.Infrastructure.Logging.TelegramDevAlerts;

/// <summary>
/// Sends warnings and errors to the developers' Telegram chat. Events are described by
/// <see cref="TelegramAlertRules"/>, bursts folded by <see cref="TelegramAlertGrouper"/>, and the
/// messages leave one by one through a queue — a burst used to be dropped while one message was in
/// flight, so the chat saw a random few of forty.
/// </summary>
public sealed class TelegramDevAlertSink : ILogEventSink, IDisposable
{
    // Telegram allows about twenty messages a minute into a group; this stays under it.
    private static readonly TimeSpan SendSpacing = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DigestCheck = TimeSpan.FromSeconds(30);
    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));

    private readonly HttpClient _httpClient;
    private readonly TelegramDevAlertOptions _options;
    private readonly string _environmentName;
    private readonly string _version;
    private readonly string? _codename;
    private readonly TelegramAlertGrouper _grouper;
    private readonly Channel<string> _outbox = Channel.CreateBounded<string>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly CancellationTokenSource _stopping = new();
    private readonly Timer _digestTimer;
    private readonly Task _sender;

    public TelegramDevAlertSink(
        TelegramDevAlertOptions options,
        string environmentName,
        string version,
        string? codename)
    {
        _options = options;
        _environmentName = environmentName;
        _version = version;
        _codename = codename;
        _grouper = new TelegramAlertGrouper(TimeSpan.FromSeconds(Math.Max(10, options.GroupWindowSeconds)));
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds))
        };
        _digestTimer = new Timer(_ => EnqueueDigests(flushAll: false), null, DigestCheck, DigestCheck);
        _sender = Task.Run(SendLoopAsync, _stopping.Token);
    }

    private bool Configured =>
        _options.Enabled && !string.IsNullOrWhiteSpace(_options.BotToken) && !string.IsNullOrWhiteSpace(_options.ChatId);

    public void Emit(LogEvent logEvent)
    {
        if (!Configured)
        {
            return;
        }

        try
        {
            var alert = TelegramAlertRules.Describe(logEvent, _environmentName, _version, _codename);
            if (_grouper.Offer(alert, DateTimeOffset.UtcNow) is { } text)
            {
                _outbox.Writer.TryWrite(text);
            }
        }
        catch
        {
            // Logging sinks must never throw back into application code; a message that cannot be
            // described or redacted in time is not sent at all.
        }
    }

    public void Dispose()
    {
        _digestTimer.Dispose();
        EnqueueDigests(flushAll: true);
        _outbox.Writer.TryComplete();
        // Give what is queued a moment to leave on shutdown, without holding the host up.
        using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            _sender.Wait(grace.Token);
        }
        catch (OperationCanceledException)
        {
            // The grace period ran out; whatever is still queued is dropped with the host.
        }
        catch (AggregateException)
        {
            // The loop ended on its own error; nothing left to wait for.
        }
        _stopping.Cancel();
        _stopping.Dispose();
        _httpClient.Dispose();
    }

    private void EnqueueDigests(bool flushAll)
    {
        try
        {
            foreach (var digest in _grouper.Due(DateTimeOffset.UtcNow, _environmentName, flushAll))
            {
                _outbox.Writer.TryWrite(digest);
            }
        }
        catch
        {
            // Same as Emit: never into the application.
        }
    }

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var text in _outbox.Reader.ReadAllAsync(_stopping.Token))
            {
                await SendAsync(text);
                await Task.Delay(SendSpacing, _stopping.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task SendAsync(string html)
    {
        try
        {
            var maxLength = Math.Clamp(_options.MaxMessageLength, 512, 3900);
            if (html.Length <= maxLength)
            {
                using var response = await PostAsync(html, "HTML");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }

            // Too long, or markup Telegram would not take: the same words without it, cut to size.
            using var _ = await PostAsync(Truncate(PlainText(html), maxLength), parseMode: null);
        }
        catch
        {
            // A failed alert is lost; the log itself still has the event.
        }
    }

    private Task<HttpResponseMessage> PostAsync(string text, string? parseMode) =>
        _httpClient.PostAsJsonAsync(BuildEndpoint(), new SendMessageRequest(
            _options.ChatId!,
            text,
            parseMode,
            DisableWebPagePreview: true,
            _options.DisableNotification), _stopping.Token);

    private string BuildEndpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.telegram.org"
            : _options.BaseUrl.TrimEnd('/');

        return $"{baseUrl}/bot{_options.BotToken}/sendMessage";
    }

    private static string PlainText(string html) => WebUtility.HtmlDecode(Tags.Replace(html, string.Empty));

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 20), "\n...[truncated]");

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParseMode,
        [property: JsonPropertyName("disable_web_page_preview")] bool DisableWebPagePreview,
        [property: JsonPropertyName("disable_notification")] bool DisableNotification);
}
