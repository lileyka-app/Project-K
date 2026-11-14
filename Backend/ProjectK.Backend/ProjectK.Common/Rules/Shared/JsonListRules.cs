using System.Text.Json;

namespace ProjectK.Common.Rules.Shared;

/// <summary>How a list of keys kept in one column as a JSON array is read back.</summary>
public static class JsonListRules
{
    /// <summary>
    /// The strings in the array; empty for a blank column and for one that does not read as an array
    /// of strings, so a damaged row falls back to the default instead of failing the read.
    /// </summary>
    public static IReadOnlyList<string> ReadStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
