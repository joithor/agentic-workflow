using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>A <see cref="DateTimeOffset"/> written as UTC ISO-8601 ('O' format, 'Z'); see <see cref="UtcTimestamps"/>.</summary>
internal sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcTimestamps.Read(ref reader);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcTimestamps.Format(value.UtcDateTime));
}
