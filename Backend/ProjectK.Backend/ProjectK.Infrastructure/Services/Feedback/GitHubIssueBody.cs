using System.Text;
using ProjectK.Common.Models.Records;

namespace ProjectK.Infrastructure.Services.Feedback;

/// <summary>
/// The Markdown an issue opened from the app carries. What the person wrote goes first and
/// verbatim, but with every <c>@name</c> defused: the tracker is public and a report must not be
/// able to page somebody. The context table at the end is what the maintainer needs to reproduce.
/// </summary>
public static class GitHubIssueBody
{
    /// <summary>Zero-width joiner after the at-sign: reads the same, no longer a mention.</summary>
    private const string DefusedAt = "@\u200D";

    public static string Compose(ProblemReport report, string? apiVersion)
    {
        var body = new StringBuilder();

        body.AppendLine("## Що сталося");
        body.AppendLine();
        body.AppendLine(Defuse(report.Description));
        body.AppendLine();

        if (report.Steps is not null)
        {
            body.AppendLine("## Кроки, щоб відтворити");
            body.AppendLine();
            body.AppendLine(Defuse(report.Steps));
            body.AppendLine();
        }

        if (report.Expected is not null)
        {
            body.AppendLine("## Що мало статися");
            body.AppendLine();
            body.AppendLine(Defuse(report.Expected));
            body.AppendLine();
        }

        // A picture pasted into the text is already there; the section is for the ones attached
        // beside it, so nothing shows twice.
        var written = string.Concat(report.Description, report.Steps, report.Expected);
        var attachedOnly = report.ScreenshotUrls.Where(url => !written.Contains(url, StringComparison.Ordinal)).ToList();
        if (attachedOnly.Count > 0)
        {
            body.AppendLine("## Скриншоти");
            body.AppendLine();
            var n = 0;
            foreach (var url in attachedOnly)
            {
                n++;
                body.AppendLine($"![скриншот {n}]({url})");
                body.AppendLine();
            }
        }

        body.AppendLine("## Контекст");
        body.AppendLine();
        body.AppendLine("| | |");
        body.AppendLine("|---|---|");
        body.AppendLine($"| Сторінка | `{Cell(report.Route) ?? "—"}` |");
        body.AppendLine($"| Застосунок | `{Cell(report.AppVersion) ?? "—"}` |");
        body.AppendLine($"| API | `{Cell(apiVersion) ?? "—"}` |");
        body.AppendLine($"| Роль | {(report.Roles.Count == 0 ? "—" : string.Join(", ", report.Roles.Select(r => $"`{Cell(r)}`")))} |");
        body.AppendLine($"| Курінь | `{(report.KurinKey is { } k ? k.ToString() : "—")}` |");
        body.AppendLine($"| Акаунт | `{report.ReporterUserKey}` |");
        body.AppendLine($"| Браузер | {Cell(report.UserAgent) ?? "—"} |");
        body.AppendLine($"| Надіслано | {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC |");
        body.AppendLine();
        body.AppendLine("_Відкрито з застосунку через «Повідомити про проблему»._");

        return body.ToString();
    }

    public static string Defuse(string markdown) => markdown.Replace("@", DefusedAt, StringComparison.Ordinal);

    /// <summary>One line, no pipes, so the context table stays a table whatever the client sent.</summary>
    private static string? Cell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var oneLine = value.Replace("\r", string.Empty).Replace("\n", " ").Replace("|", "\\|").Replace("`", "'");
        return oneLine.Length <= 300 ? oneLine : oneLine[..300] + "…";
    }
}
