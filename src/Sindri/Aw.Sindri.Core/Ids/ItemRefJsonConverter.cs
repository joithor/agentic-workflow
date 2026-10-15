using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Ids;

/// <summary>Writes an <see cref="ItemRef"/> as its text and reads it back through <see cref="ItemRef.TryParse"/> (a JsonException, so a 400, when it is neither form).</summary>
public sealed class ItemRefJsonConverter : JsonConverter<ItemRef>
{
    /// <inheritdoc/>
    public override ItemRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && ItemRef.TryParse(reader.GetString(), out var value)
            ? value
            : throw new JsonException("An item is an item id ('ITM-…') or a tracker alias ('ENG-142')");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ItemRef value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
