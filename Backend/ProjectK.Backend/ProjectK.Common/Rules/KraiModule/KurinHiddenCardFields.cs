using System.Text.Json;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Rules.KraiModule;

/// <summary>How a kurin's hidden card fields are kept in one column.</summary>
public static class KurinHiddenCardFields
{
    /// <inheritdoc cref="JsonListRules.ReadStrings"/>
    public static IReadOnlyList<string> Read(string? json) => JsonListRules.ReadStrings(json);

    public static string? Write(IEnumerable<string> keys)
    {
        var list = keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return list.Count == 0 ? null : JsonSerializer.Serialize(list);
    }
}
