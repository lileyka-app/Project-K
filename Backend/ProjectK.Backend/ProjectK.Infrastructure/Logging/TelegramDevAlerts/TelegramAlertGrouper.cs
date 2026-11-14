using System.Text.RegularExpressions;

namespace ProjectK.Infrastructure.Logging.TelegramDevAlerts;

/// <summary>
/// Keeps a burst from becoming a wall of messages: the first event of a kind goes out at once, the
/// ones that follow within the window are only counted, and when the window closes one digest says
/// how many more there were and where. A scanner trying forty paths is two messages, not forty.
/// <para>
/// The same failure is often logged twice — the exception, then the request that answered 500 — so a
/// request-log line whose trace already raised an alert is dropped.
/// </para>
/// </summary>
public sealed class TelegramAlertGrouper
{
    private readonly TimeSpan _window;
    private readonly object _lock = new();
    private readonly Dictionary<string, Group> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _alertedTraces = new(StringComparer.Ordinal);

    public TelegramAlertGrouper(TimeSpan window)
    {
        _window = window;
    }

    /// <summary>The text to send now, or null when the event only joins a group already announced.</summary>
    public string? Offer(TelegramAlert alert, DateTimeOffset now)
    {
        lock (_lock)
        {
            ForgetOldTraces(now);
            if (alert.IsRequestLog && alert.TraceId is { } requestTrace && _alertedTraces.ContainsKey(requestTrace))
            {
                return null;
            }

            if (_groups.TryGetValue(alert.GroupKey, out var group) && now - group.StartedAt < _window)
            {
                group.More++;
                if (alert.Path is { } path && group.Paths.Count < MaxTrackedPaths)
                {
                    group.Paths.Add(path);
                }
                return null;
            }

            _groups[alert.GroupKey] = new Group(alert, now);
            if (alert.TraceId is { } trace)
            {
                _alertedTraces[trace] = now;
            }
            return alert.Text;
        }
    }

    /// <summary>Digests of every group whose window has closed with something left unsaid; the groups are forgotten.</summary>
    public IReadOnlyList<string> Due(DateTimeOffset now, string environmentName, bool flushAll = false)
    {
        lock (_lock)
        {
            var digests = new List<string>();
            foreach (var (key, group) in _groups.ToList())
            {
                if (!flushAll && now - group.StartedAt < _window)
                {
                    continue;
                }

                if (group.More > 0)
                {
                    digests.Add(TelegramAlertRules.Digest(environmentName, group.First, group.More, group.Paths, _window));
                }
                _groups.Remove(key);
            }
            return digests;
        }
    }

    private void ForgetOldTraces(DateTimeOffset now)
    {
        foreach (var (trace, seenAt) in _alertedTraces.ToList())
        {
            if (now - seenAt > TraceMemory)
            {
                _alertedTraces.Remove(trace);
            }
        }
    }

    private const int MaxTrackedPaths = 50;
    private static readonly TimeSpan TraceMemory = TimeSpan.FromMinutes(2);

    private sealed class Group(TelegramAlert first, DateTimeOffset startedAt)
    {
        public TelegramAlert First { get; } = first;
        public DateTimeOffset StartedAt { get; } = startedAt;
        public int More { get; set; }
        public SortedSet<string> Paths { get; } = first.Path is { } path ? new([path], StringComparer.Ordinal) : new(StringComparer.Ordinal);
    }
}

/// <summary>Emails and secrets never leave in an alert, whatever field they hid in.</summary>
public static class RedactionRules
{
    // A message that takes longer than this to redact is dropped rather than sent half-redacted.
    private static readonly TimeSpan RedactTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RedactTimeout);

    private static readonly Regex TokenRegex = new(
        @"(?i)(bearer\s+)[a-z0-9._~+/=-]+|(?i)(token|password|secret|apikey|api_key|code)=([^\s;&<]+)",
        RegexOptions.Compiled,
        RedactTimeout);

    public static string Apply(string value)
    {
        var redacted = EmailRegex.Replace(value, "[email]");
        return TokenRegex.Replace(redacted, match =>
        {
            if (match.Groups[1].Success)
            {
                return $"{match.Groups[1].Value}[redacted]";
            }

            if (match.Groups[2].Success)
            {
                return $"{match.Groups[2].Value}=[redacted]";
            }

            return "[redacted]";
        });
    }
}
