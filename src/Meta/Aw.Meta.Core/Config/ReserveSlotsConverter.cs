using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>Reads and writes a tier's slots: a whole number, or the string "all".</summary>
public sealed class ReserveSlotsConverter : JsonConverter<ReserveSlots>
{
    private const string AllText = "all";

    /// <inheritdoc />
    public override ReserveSlots Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Parse(JsonElement.ParseValue(ref reader));

    /// <summary>Reads a tier's slots from a parsed value.</summary>
    /// <param name="element">A whole number, or the string "all".</param>
    /// <returns>The slots.</returns>
    internal static ReserveSlots Parse(JsonElement element) => element switch
    {
        { ValueKind: JsonValueKind.Number } when element.TryGetInt32(out var count) && count >= 0 => new ReserveSlots(count),
        { ValueKind: JsonValueKind.String } when string.Equals(element.GetString(), AllText, StringComparison.OrdinalIgnoreCase) => ReserveSlots.All,
        _ => throw new JsonException("slots must be a whole number or \"all\""),
    };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ReserveSlots value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value.Count is { } count)
        {
            writer.WriteNumberValue(count);
        }
        else
        {
            writer.WriteStringValue(AllText);
        }
    }
}
