using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Infrastructure.Services.Feedback;

namespace ProjectK.Infrastructure.Tests.Services;

/// <summary>
/// The issue a report becomes: the person's words first, defused of mentions, then the context
/// the maintainer reproduces from.
/// </summary>
public class GitHubIssueBodyRulesTests
{
    private static ProblemReport Report(string description = "Не зберігається", string? steps = "1. Натиснути", string? expected = null, params string[] screenshots) =>
        new("Заголовок", description, steps, expected, "/member/1", "v1.0.0", "Mozilla | Chrome", Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"), ["Manager", "Member"], screenshots);

    [Fact]
    public void Compose_ShouldPutTheWordsFirst_ThenScreenshots_ThenContext()
    {
        var body = GitHubIssueBodyRules.Compose(Report(expected: "Має зберегтись", screenshots: "https://blob/feedback-screenshots/a.png"), "v1.0.0-api");

        var words = body.IndexOf("Не зберігається", StringComparison.Ordinal);
        var steps = body.IndexOf("## Кроки, щоб відтворити", StringComparison.Ordinal);
        var expected = body.IndexOf("Має зберегтись", StringComparison.Ordinal);
        var shots = body.IndexOf("![скриншот 1](https://blob/feedback-screenshots/a.png)", StringComparison.Ordinal);
        var context = body.IndexOf("## Контекст", StringComparison.Ordinal);

        Assert.True(words > 0 && words < steps && steps < expected && expected < shots && shots < context);
        Assert.Contains("| API | `v1.0.0-api` |", body);
        Assert.Contains("| Застосунок | `v1.0.0` |", body);
        Assert.Contains("| Сторінка | `/member/1` |", body);
        Assert.Contains("`Manager`, `Member`", body);
        Assert.Contains("22222222-2222-2222-2222-222222222222", body);
        Assert.Contains("11111111-1111-1111-1111-111111111111", body);
        // A pipe in the user agent must not break the table.
        Assert.Contains("Mozilla \\| Chrome", body);
    }

    // Pasted inline and attached beside: the same picture is not shown twice.
    [Fact]
    public void Compose_ShouldNotRepeatAPictureAlreadyInTheText()
    {
        var inline = "https://blob/feedback-screenshots/inline.png";
        var body = GitHubIssueBodyRules.Compose(
            Report(description: $"Ось:\n![скриншот]({inline})", screenshots: [inline, "https://blob/feedback-screenshots/other.png"]),
            null);

        Assert.Equal(1, body.Split(inline).Length - 1);
        Assert.Contains("![скриншот 1](https://blob/feedback-screenshots/other.png)", body);
    }

    [Fact]
    public void Compose_ShouldSkipEmptySections()
    {
        var body = GitHubIssueBodyRules.Compose(Report(steps: null), null);

        Assert.DoesNotContain("## Кроки", body);
        Assert.DoesNotContain("## Що мало статися", body);
        Assert.DoesNotContain("## Скриншоти", body);
        Assert.Contains("| API | `—` |", body);
    }

    // The tracker is public: a report must not be able to page a GitHub account.
    [Fact]
    public void Compose_ShouldDefuseMentions_ButKeepTheTextReadable()
    {
        var body = GitHubIssueBodyRules.Compose(Report(description: "Пише @iamavasya і @all про це"), null);

        Assert.DoesNotContain("@iamavasya", body, StringComparison.Ordinal);
        Assert.Contains("@‍iamavasya", body);
        Assert.Contains("@‍all", body);
    }
}
