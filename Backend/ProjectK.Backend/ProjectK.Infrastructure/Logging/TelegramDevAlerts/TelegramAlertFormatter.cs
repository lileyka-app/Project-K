using System.Text;
using System.Text.RegularExpressions;
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

/// <summary>
/// Turns a log event into a Telegram message a person can read at a glance: what happened in words,
/// who and where, only the fields that carry something, and for an error where in our code it broke.
/// HTML parse mode; everything taken from the event is escaped.
/// </summary>
public static class TelegramAlertFormatter
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Paths no part of this app serves: someone is looking for a leaked repository, config or panel.</summary>
    private static readonly Regex ProbePath = new(
        @"(^|/)(\.git|\.env|\.svn|\.hg|\.aws|\.ssh|\.ds_store|\.vscode|\.idea|wp-|wordpress|xmlrpc|phpmyadmin|pma|adminer|cgi-bin|vendor/phpunit|actuator|server-status|\.well-known/security\.txt$)|\.(php|asp|aspx|jsp|cgi|bak|old|sql|zip|tar|gz|ini|yml|yaml)$|credentials|config\.json$|docker-compose",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Dictionary<string, string> ActionTitles = new(StringComparer.Ordinal)
    {
        ["RateLimit.Rejected"] = "Ліміт запитів перевищено",
        ["Auth.LoginFailedThreshold"] = "Забагато невдалих входів",
        ["Auth.MfaFailedThreshold"] = "Забагато невдалих кодів 2FA",
        ["Auth.IpChanged"] = "Вхід з нової IP-адреси",
        ["Geo.Blocked"] = "Запит заблоковано за країною"
    };

    public static TelegramAlert Describe(LogEvent logEvent, string environmentName, string version, string? codename)
    {
        var payload = ReadPayload(logEvent);
        string? Prop(string name) => payload.TryGetValue(name, out var value) ? value : Scalar(logEvent, name);

        var eventType = Scalar(logEvent, "EventType");
        var action = Prop("Action");
        var isSecurity = eventType == "Security.Suspicious";
        var isRequestLog = logEvent.MessageTemplate.Text.StartsWith("HTTP {RequestMethod}", StringComparison.Ordinal);

        var method = Prop("Method") ?? Prop("RequestMethod");
        var path = Prop("Path") ?? Prop("RequestPath");
        var query = Prop("Query");
        var ip = Prop("Ip");
        var country = Prop("CountryCode");
        var traceId = Prop("TraceId") ?? Prop("RequestId") ?? Prop("CorrelationId");
        var isProbe = !string.IsNullOrWhiteSpace(path) && SafeMatch(ProbePath, path);

        var title = TitleOf(logEvent, isSecurity, action);

        var text = new StringBuilder()
            .Append(LevelMark(logEvent.Level)).Append(' ')
            .Append("<b>").Append(Escape(environmentName.ToUpperInvariant())).Append(" · ").Append(Escape(title)).AppendLine("</b>");

        if (isProbe)
        {
            text.AppendLine("<i>Схоже на сканер: шукає службові файли, яких тут немає. Відбито — дій не треба.</i>");
        }

        text.AppendLine();

        AppendMessage(text, logEvent, isSecurity);
        AppendRequest(text, method, path, query, Prop("StatusCode"));

        Field(text, "Хост", Prop("Host") ?? Prop("RequestHost"));
        if (!string.IsNullOrWhiteSpace(ip) || !string.IsNullOrWhiteSpace(country))
        {
            Field(text, "IP", string.Join(" · ", new[] { ip, country }.Where(v => !string.IsNullOrWhiteSpace(v))));
        }
        Field(text, "Зміна IP", Join(Prop("OldIp"), Prop("NewIp"), " → "));
        Field(text, "Клієнт", Prop("UserAgent"), code: true);

        if (isSecurity || Prop("ActorUserId") is not null)
        {
            Field(text, "Хто", Prop("ActorUserId") ?? (Prop("Email") is { } email ? email : "анонім"), code: Prop("ActorUserId") is not null);
        }

        Field(text, "Спроб", Prop("Count"));
        Field(text, "Ліміт", Prop("PolicyName"));
        if (isSecurity && action is not null && !ActionTitles.ContainsKey(action))
        {
            Field(text, "Причина", Prop("Reason"));
        }

        AppendFrames(text, logEvent.Exception);

        if (!isSecurity && Scalar(logEvent, "SourceContext") is { } source)
        {
            Field(text, "Джерело", source[(source.LastIndexOf('.') + 1)..], code: true);
        }

        AppendFooter(text, version, codename, traceId);

        var groupKey = GroupKeyOf(logEvent, isSecurity, action, ip, path);

        return new TelegramAlert(groupKey, Redaction.Apply(text.ToString()), title, logEvent.Level, ip, country, path, traceId, isRequestLog);
    }

    private static string TitleOf(LogEvent logEvent, bool isSecurity, string? action)
    {
        if (isSecurity)
        {
            return ActionTitles.GetValueOrDefault(action ?? string.Empty, action ?? "Підозріла подія");
        }

        return logEvent.Exception is { } ex ? $"Помилка: {ex.GetType().Name}" : LevelTitle(logEvent.Level);
    }

    private static string GroupKeyOf(LogEvent logEvent, bool isSecurity, string? action, string? ip, string? path)
    {
        if (isSecurity)
        {
            return $"sec|{action}|{ip}";
        }

        return logEvent.Exception is { } e
            ? $"err|{e.GetType().FullName}|{Shorten(e.Message, 120)}|{path}"
            : $"log|{logEvent.Level}|{logEvent.MessageTemplate.Text}|{path}";
    }

    private static void AppendMessage(StringBuilder text, LogEvent logEvent, bool isSecurity)
    {
        if (logEvent.Exception is { } exception)
        {
            text.AppendLine(Escape(Shorten(exception.Message, 400)));
            if (exception.InnerException is { } inner)
            {
                text.Append("<b>Причина:</b> ").Append(Escape(inner.GetType().Name)).Append(": ").AppendLine(Escape(Shorten(inner.Message, 300)));
            }
        }
        else if (!isSecurity)
        {
            text.AppendLine(Escape(Shorten(logEvent.RenderMessage(), 600)));
        }
    }

    private static void AppendRequest(StringBuilder text, string? method, string? path, string? query, string? status)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var request = $"{method} {path}{query}".Trim();
        text.Append("<b>Запит:</b> <code>").Append(Escape(Shorten(request, 300))).Append("</code>");
        if (!string.IsNullOrWhiteSpace(status))
        {
            text.Append(" → ").Append(Escape(status));
        }
        text.AppendLine();
    }

    private static void AppendFrames(StringBuilder text, Exception? exception)
    {
        if (exception is null || OurFrames(exception) is not { Count: > 0 } frames)
        {
            return;
        }

        text.AppendLine("<b>Де:</b>");
        foreach (var frame in frames)
        {
            text.Append("<code>").Append(Escape(frame)).AppendLine("</code>");
        }
    }

    private static void AppendFooter(StringBuilder text, string version, string? codename, string? traceId)
    {
        text.AppendLine();
        text.Append("<i>").Append(Escape(version));
        if (!string.IsNullOrWhiteSpace(codename))
        {
            text.Append(" «").Append(Escape(codename)).Append('»');
        }
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            text.Append(" · trace ").Append(Escape(traceId));
        }
        text.Append("</i>");
    }

    /// <summary>
    /// «Ще 36 таких за 5 хв» — what the alert would have repeated, in one message: how many, from where,
    /// and which paths, the first few of them.
    /// </summary>
    public static string Digest(string environmentName, TelegramAlert first, int more, IReadOnlyCollection<string> paths, TimeSpan window)
    {
        var text = new StringBuilder()
            .Append(LevelMark(first.Level)).Append(' ')
            .Append("<b>").Append(Escape(environmentName.ToUpperInvariant())).Append(" · ").Append(Escape(first.Title))
            .Append(" · ще ").Append(more).Append(" за ").Append((int)Math.Ceiling(window.TotalMinutes)).AppendLine(" хв</b>");

        if (!string.IsNullOrWhiteSpace(first.Ip) || !string.IsNullOrWhiteSpace(first.Country))
        {
            text.Append("<b>IP:</b> ").AppendLine(Escape(string.Join(" · ", new[] { first.Ip, first.Country }.Where(v => !string.IsNullOrWhiteSpace(v)))));
        }

        if (paths.Count > 0)
        {
            var probes = paths.Count(p => SafeMatch(ProbePath, p));
            text.Append("<b>Шляхи</b> (").Append(paths.Count).Append(probes == paths.Count ? ", усі — пошук службових файлів" : string.Empty).AppendLine("):");
            foreach (var path in paths.Take(DigestPaths))
            {
                text.Append("<code>").Append(Escape(Shorten(path, 120))).AppendLine("</code>");
            }
            if (paths.Count > DigestPaths)
            {
                text.Append("… і ще ").Append(paths.Count - DigestPaths).AppendLine();
            }
        }

        return Redaction.Apply(text.ToString().TrimEnd());
    }

    public const int DigestPaths = 8;

    private static string LevelMark(LogEventLevel level) => level switch
    {
        >= LogEventLevel.Error => "🔴",
        LogEventLevel.Warning => "🟠",
        _ => "🔵"
    };

    private static string LevelTitle(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal => "Критична помилка",
        LogEventLevel.Error => "Помилка",
        LogEventLevel.Warning => "Попередження",
        _ => "Подія"
    };

    /// <summary>The first frames that are ours: where in ProjectK it broke, not where in the framework it surfaced.</summary>
    private static IReadOnlyList<string> OurFrames(Exception exception)
    {
        var trace = exception.StackTrace;
        if (string.IsNullOrWhiteSpace(trace))
        {
            return [];
        }

        return trace
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("at ProjectK.", StringComparison.Ordinal))
            .Select(line => Regex.Replace(line, @"^at |\(.*?\)| in .*?([^\\/]+:line \d+)$", m => m.Groups[1].Success ? $" ({m.Groups[1].Value})" : string.Empty, RegexOptions.None, RegexTimeout))
            .Select(line => Shorten(line, 160))
            .Distinct()
            .Take(3)
            .ToList();
    }

    private static void Field(StringBuilder text, string label, string? value, bool code = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        text.Append("<b>").Append(label).Append(":</b> ");
        text.AppendLine(code ? $"<code>{Escape(value)}</code>" : Escape(value));
    }

    private static string? Join(string? a, string? b, string separator) =>
        string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b) ? null : $"{a ?? "—"}{separator}{b ?? "—"}";

    private static Dictionary<string, string> ReadPayload(LogEvent logEvent)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (logEvent.Properties.TryGetValue("Payload", out var payload) && payload is StructureValue structure)
        {
            foreach (var property in structure.Properties)
            {
                if (property.Value is ScalarValue { Value: not null } scalar && scalar.Value.ToString() is { Length: > 0 } text)
                {
                    values[property.Name] = text;
                }
            }
        }

        return values;
    }

    private static string? Scalar(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: not null } scalar
            ? scalar.Value.ToString()
            : null;

    private static bool SafeMatch(Regex regex, string value)
    {
        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");

    /// <summary>Telegram's HTML needs only these three escaped; the rest of the text stays as it is written.</summary>
    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
