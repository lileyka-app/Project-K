using System.Text.Json;

namespace ProjectK.Common.Rules.KraiModule;

/// <summary>How a member's values of their край's own card fields are kept in one column.</summary>
public static class MemberCardExtraRules
{
    public static Dictionary<string, string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) is { } values
                ? new Dictionary<string, string>(values, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public static string? Write(IReadOnlyDictionary<string, string> values)
    {
        return values.Count == 0
            ? null
            : JsonSerializer.Serialize(values.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}
