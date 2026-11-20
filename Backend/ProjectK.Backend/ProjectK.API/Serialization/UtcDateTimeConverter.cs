using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectK.API.Serialization;

/// <summary>
/// Writes every <see cref="DateTime"/> as UTC.
/// <para>
/// Every timestamp in the database is stored as UTC, but EF Core hands <c>datetime2</c> columns back
/// with <see cref="DateTimeKind.Unspecified"/>, and System.Text.Json then writes them with no trailing
/// <c>Z</c>. The browser reads such a value as local time, so a UTC timestamp arrived at the client
/// shifted by the viewer's offset — three hours in Ukraine. Only the agenda's response factory marked
/// its dates by hand; nine other response types did not.
/// </para>
/// <para>
/// Reading is left at the default so request payloads keep the meaning they have always had.
/// </para>
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value));

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
