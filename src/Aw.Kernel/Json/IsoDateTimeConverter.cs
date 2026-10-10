using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>
/// A <see cref="DateTime"/> written exactly as <see cref="IsoTimestampConverter"/> writes a timestamp. Only
/// <see cref="DateTimeKind.Utc"/> values may be written: a local or unspecified time would depend on the machine's
/// time zone, so writing one is a bug and throws. Reading accepts only the toISOString form and returns UTC.
/// </summary>
internal sealed class IsoDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return DateTime.TryParseExact(text, IsoTimestampConverter.Format, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : throw new JsonException($"Expected a timestamp in Date#toISOString form (2026-10-10T06:37:59.007Z), found '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException(
                $"DateTime of kind {value.Kind} can't be written as a Node timestamp; use DateTimeKind.Utc or DateTimeOffset.");
        }

        var truncated = value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));
        writer.WriteStringValue(truncated.ToString(IsoTimestampConverter.Format, CultureInfo.InvariantCulture));
    }
}
