namespace ProjectK.Common.Rules.Shared;

/// <summary>How free text a person typed is kept.</summary>
public static class TextRules
{
    /// <summary>The text trimmed at both ends, or <c>null</c> when nothing but whitespace is left.</summary>
    public static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
