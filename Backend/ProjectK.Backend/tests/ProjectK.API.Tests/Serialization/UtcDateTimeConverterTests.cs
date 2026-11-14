using System.Text.Json;
using ProjectK.API.Serialization;

namespace ProjectK.API.Tests.Serialization;

public class UtcDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new NullableUtcDateTimeConverter());
        return options;
    }

    [Fact]
    public void UnspecifiedKind_IsWrittenAsUtc()
    {
        // What EF hands back for a datetime2 column, and what used to reach the browser with no
        // zone marker at all.
        var stored = new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Unspecified);

        Assert.Equal("\"2026-08-26T09:30:00Z\"", JsonSerializer.Serialize(stored, Options));
    }

    [Fact]
    public void UtcKind_IsUnchanged()
    {
        var value = new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Utc);

        Assert.Equal("\"2026-08-26T09:30:00Z\"", JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void LocalKind_IsConvertedRatherThanRelabelled()
    {
        var local = new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Local);

        var expected = "\"" + local.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss") + "Z\"";
        Assert.Equal(expected, JsonSerializer.Serialize(local, Options));
    }

    [Fact]
    public void NullableValue_IsMarkedTheSameWay()
    {
        DateTime? stored = new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Unspecified);

        Assert.Equal("\"2026-08-26T09:30:00Z\"", JsonSerializer.Serialize(stored, Options));
    }

    [Fact]
    public void NullValue_StaysNull()
    {
        DateTime? missing = null;

        Assert.Equal("null", JsonSerializer.Serialize(missing, Options));
    }

    [Fact]
    public void Reading_KeepsTheDefaultMeaning()
    {
        var parsed = JsonSerializer.Deserialize<DateTime>("\"2026-08-26T09:30:00Z\"", Options);

        Assert.Equal(new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Utc), parsed.ToUniversalTime());
    }
}
