using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;

namespace Aw.Kernel.Json;

/// <summary>
/// Reads and writes an int-backed typed id or constrained value as a JSON number, or as a dictionary key in its
/// canonical invariant form ("1200"). Null, a string, a non-integer, a non-canonical key ("01", "+1") or a value the
/// type refuses is a <see cref="JsonException"/> (an invalid-input error through the Kernel JSON profile).
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class Int32IdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, ITypedId<TId, int>
{
    /// <inheritdoc/>
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value) && TId.TryFrom(value, out var id)
            ? id
            : throw TypedIdJson.Failure<TId>(reader.TokenType, JsonTokenType.Number, "a number");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Value);
    }

    /// <inheritdoc/>
    public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TypedIdJson.TryParseInt32(reader.GetString(), out var value) && TId.TryFrom(value, out var id) ? id : throw TypedIdJson.Refused<TId>();

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
