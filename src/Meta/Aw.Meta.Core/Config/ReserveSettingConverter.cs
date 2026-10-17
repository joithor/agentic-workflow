using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>Reads and writes the reserve setting: an array of tiers, true (the defaults) or false (off). Written by hand so it needs no reflection.</summary>
public sealed class ReserveSettingConverter : JsonConverter<ReserveSetting>
{
    /// <inheritdoc />
    public override ReserveSetting Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);
        return element.ValueKind switch
        {
            JsonValueKind.False => ReserveSetting.Off,
            JsonValueKind.True => ReserveSetting.Default,
            JsonValueKind.Array => new ReserveSetting(Enabled: true, [.. element.EnumerateArray().Select(Tier)]),
            JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null => throw new JsonException("reserve must be an array of {\"minDepth\", \"slots\"} tiers, or a boolean"),
            _ => throw new JsonException("reserve must be an array of tiers, or a boolean"),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ReserveSetting value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (!value.Enabled)
        {
            writer.WriteBooleanValue(value: false);
            return;
        }

        var slots = new ReserveSlotsConverter();
        writer.WriteStartArray();
        foreach (var tier in value.Tiers)
        {
            writer.WriteStartObject();
            writer.WriteNumber("minDepth", tier.MinDepth);
            writer.WritePropertyName("slots");
            slots.Write(writer, tier.Slots, options);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static ReserveTier Tier(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("minDepth", out var depth) || depth.ValueKind != JsonValueKind.Number || !depth.TryGetInt32(out var minDepth) || minDepth < 0
            || !element.TryGetProperty("slots", out var slots))
        {
            throw new JsonException("each reserve tier must be an object with a whole-number minDepth and slots");
        }

        return new ReserveTier(minDepth, ReserveSlotsConverter.Parse(slots));
    }
}
