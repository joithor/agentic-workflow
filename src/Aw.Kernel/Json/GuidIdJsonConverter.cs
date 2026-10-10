using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;

namespace Aw.Kernel.Json;

/// <summary>
/// Reads and writes a Guid-backed typed id as a string in its one canonical form, "D" lowercase, as a value or a
/// dictionary key. Null, an empty string, another token, another Guid layout, uppercase hex or a value the type
/// refuses is a <see cref="JsonException"/> (an invalid-input error through the Kernel JSON profile).
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class GuidIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, ITypedId<TId, Guid>
{
    /// <inheritdoc/>
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String && TypedIdJson.TryParseGuid(reader.GetString(), out var value) && TId.TryFrom(value, out var id)
            ? id
            : throw TypedIdJson.Failure<TId>(reader.TokenType, JsonTokenType.String, "a string");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }

    /// <inheritdoc/>
    public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TypedIdJson.TryParseGuid(reader.GetString(), out var value) && TId.TryFrom(value, out var id) ? id : throw TypedIdJson.Refused<TId>();

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.Value.ToString("D"));
    }
}
