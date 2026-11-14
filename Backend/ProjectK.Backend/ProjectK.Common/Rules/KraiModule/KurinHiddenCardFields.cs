using System.Text.Json;

namespace ProjectK.Common.Rules.KraiModule;

/// <summary>How a kurin's hidden card fields are kept in one column.</summary>
public static class KurinHiddenCardFields
{
    public static IReadOnlyList<string> Read(string? json)
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

    public static string? Write(IEnumerable<string> keys)
    {
        var list = keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return list.Count == 0 ? null : JsonSerializer.Serialize(list);
    }
}
