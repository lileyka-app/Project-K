using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectK.API.Serialization;

/// <summary>
/// The nullable counterpart of <see cref="UtcDateTimeConverter"/>, registered explicitly so a
/// <c>DateTime?</c> property is marked the same way as a non-nullable one.
/// </summary>
public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(UtcDateTimeConverter.ToUtc(value.Value));
    }
}
