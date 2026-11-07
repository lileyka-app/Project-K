using System.Text.RegularExpressions;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// Reads the pictures out of a project's Markdown. Only the project's own pictures may stand
/// there: an outside address would show a stranger's file, and tell its host who looked.
/// </summary>
public static partial class ProbeProjectMarkdown
{
    [GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]*)\)")]
    private static partial Regex ImagePattern();

    /// <summary>
    /// The keys of the pictures in <paramref name="texts"/>, in order; null when a picture points
    /// anywhere but at <c>project-image:{key}</c>.
    /// </summary>
    public static IReadOnlyList<Guid>? ImageKeys(params string[] texts)
    {
        var keys = new List<Guid>();
        foreach (Match match in texts.SelectMany(text => ImagePattern().Matches(text)))
        {
            var target = match.Groups[1].Value;
            if (!target.StartsWith(ProbeProjectRules.ImageScheme, StringComparison.Ordinal)
                || !Guid.TryParse(target[ProbeProjectRules.ImageScheme.Length..], out var key))
            {
                return null;
            }

            keys.Add(key);
        }

        return keys;
    }
}
