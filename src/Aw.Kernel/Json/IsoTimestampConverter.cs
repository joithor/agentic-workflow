using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>
/// Timestamps exactly as 'Date#toISOString' writes them: UTC, exactly three fractional digits, 'Z'.
/// Sub-millisecond ticks are truncated (a JS Date has millisecond precision). Reading accepts only that form.
/// </summary>
internal sealed class IsoTimestampConverter : JsonConverter<DateTimeOffset>
{
    internal const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return DateTimeOffset.TryParseExact(text, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : throw new JsonException($"Expected a timestamp in Date#toISOString form (2026-10-10T06:37:59.007Z), found '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var utc = value.UtcDateTime;
        var truncated = utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMillisecond));
        writer.WriteStringValue(truncated.ToString(Format, CultureInfo.InvariantCulture));
    }
}
