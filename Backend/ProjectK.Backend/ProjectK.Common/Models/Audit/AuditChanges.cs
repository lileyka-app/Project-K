using System.Globalization;
using ProjectK.Common.Models.Members;

namespace ProjectK.Common.Models.Audit;

/// <summary>
/// Builds the changes of a journal entry. Only fields that actually changed are kept, values are
/// written the same way everywhere, and a field that <see cref="MemberDataFields"/> counts as a
/// person's private data is recorded as changed without its values.
/// </summary>
public static class AuditChanges
{
    /// <summary>The fields among <paramref name="fields"/> whose value differs, as changes.</summary>
    public static IReadOnlyList<AuditChange> Of(params (string Field, object? From, object? To)[] fields)
    {
        var changes = new List<AuditChange>();
        foreach (var (field, from, to) in fields)
        {
            var before = Format(from);
            var after = Format(to);
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new AuditChange(field, before, after).Sanitized());
        }

        return changes;
    }

    /// <summary>What something was made with: every field that has a value, from nothing.</summary>
    public static IReadOnlyList<AuditChange> Added(params (string Field, object? Value)[] fields) =>
        Of(fields.Select(field => (field.Field, (object?)null, field.Value)).ToArray());

    /// <summary>What something held when it was removed: every field that had a value, to nothing.</summary>
    public static IReadOnlyList<AuditChange> Removed(params (string Field, object? Value)[] fields) =>
        Of(fields.Select(field => (field.Field, field.Value, (object?)null)).ToArray());

    /// <summary>Whether <paramref name="field"/> is a person's private data, which the journal never holds.</summary>
    public static bool IsPrivate(string field) => MemberDataFields.GroupOf(field) is not null;

    /// <summary>
    /// A value as the journal writes it: dates as <c>yyyy-MM-dd</c> (with the time only when there
    /// is one), enums by name, numbers in the invariant culture, and an empty string as no value.
    /// </summary>
    public static string? Format(object? value) => value switch
    {
        null => null,
        string text => string.IsNullOrEmpty(text) ? null : text,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime moment when moment.TimeOfDay == TimeSpan.Zero => moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime moment => DateTime.SpecifyKind(moment, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        Enum member => member.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
