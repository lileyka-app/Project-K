namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// How a running release is spelled where people read it — the banner, <c>/health</c>, the kurin
/// report and the developer alerts.
/// </summary>
/// <remarks>
/// A release used to be named <c>vX.Y.Z "Code Name"</c>, and the build pipeline substituted both
/// halves into the settings files. Since 1.0 the code name is optional, so the configured value can
/// legitimately arrive empty; without this, every one of those places printed a bare pair of quotes
/// after the version.
/// </remarks>
public static class ReleaseDisplay
{
    private const string Unknown = "unknown";

    /// <summary>The code name, or <c>null</c> when this release carries none.</summary>
    public static string? CodeName(string? configured) =>
        TextRules.TrimOrNull(configured);

    /// <summary>
    /// The version on its own, or the version followed by the code name in quotes when there is one.
    /// </summary>
    public static string Label(string? version, string? codeName)
    {
        var release = string.IsNullOrWhiteSpace(version) ? Unknown : version.Trim();
        var code = CodeName(codeName);

        return code is null ? release : $"{release} \"{code}\"";
    }
}
