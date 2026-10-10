using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>
/// A <see cref="DateTime"/> written as UTC ISO-8601. Only <see cref="DateTimeKind.Utc"/> values may be written: a
/// local or unspecified time depends on the machine's time zone, so writing one is a bug and throws.
/// </summary>
internal sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcTimestamps.Read(ref reader).UtcDateTime;

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Kind == DateTimeKind.Utc
            ? UtcTimestamps.Format(value)
            : throw new InvalidOperationException(
                $"DateTime of kind {value.Kind} can't be written; use DateTimeKind.Utc or DateTimeOffset."));
}
