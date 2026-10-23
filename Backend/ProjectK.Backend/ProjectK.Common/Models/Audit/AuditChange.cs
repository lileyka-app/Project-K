using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Audit;

/// <summary>
/// One field of a journal entry: what it was and what it became. A field of a person's private
/// data carries neither value, only <see cref="Redacted"/> — the journal says it changed, never to what.
/// </summary>
/// <param name="Field">The field, by its JSON name.</param>
/// <param name="From">The value before; null when there was none.</param>
/// <param name="To">The value after; null when there is none.</param>
/// <param name="Redacted">True when the values are private data and were left out.</param>
public sealed record AuditChange(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("from"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? From,
    [property: JsonPropertyName("to"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? To,
    [property: JsonPropertyName("redacted"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Redacted = false)
{
    /// <summary>The same change without its values when the field is private data; unchanged otherwise.</summary>
    public AuditChange Sanitized() =>
        AuditChanges.IsPrivate(Field) ? new AuditChange(Field, null, null, Redacted: true) : this;
}
