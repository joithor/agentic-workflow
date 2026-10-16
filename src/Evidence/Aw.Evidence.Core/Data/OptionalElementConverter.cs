using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// Reads and writes a JSON element that may be absent: a missing property stays null ("not given"), and a JSON null is read as a Null element,
/// so "equals null" and "no equals" stay apart.
/// </summary>
public sealed class OptionalElementConverter : JsonConverter<JsonElement?>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override JsonElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonElement.ParseValue(ref reader);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, JsonElement? value, JsonSerializerOptions options)
    {
        if (value is { } element)
        {
            element.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
