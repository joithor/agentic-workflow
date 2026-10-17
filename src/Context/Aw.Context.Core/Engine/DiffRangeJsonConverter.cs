using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>Writes a <see cref="DiffRange"/> as the one string <c xml:lang="en">base..head</c> and reads it back through <see cref="DiffRange.Parse(string?)"/>.</summary>
public sealed class DiffRangeJsonConverter : JsonConverter<DiffRange>
{
    /// <inheritdoc/>
    public override DiffRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parsed = DiffRange.Parse(reader.GetString());
        return parsed.IsSuccess ? parsed.Value : throw new JsonException(DiffRange.Message);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DiffRange value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.ToString());
    }
}
