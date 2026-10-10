using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;

namespace Aw.Kernel.Json;

/// <summary>
/// Reads and writes a string-backed typed id as its value, exactly as the options write a plain string, as a value
/// or a dictionary key. Name it on the id with '[JsonConverter(typeof(StringIdJsonConverter&lt;MyId&gt;))]'
/// (docs/csharp/typed-ids.md). Null, an empty string, another token or a value the type refuses is a
/// <see cref="JsonException"/>: an invalid-input error through the Kernel JSON profile, a 400 at an endpoint.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class StringIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, ITypedId<TId, string>
{
    /// <inheritdoc/>
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? string.Empty : string.Empty;
        return text.Length > 0 && TId.TryFrom(text, out var id)
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
    public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? string.Empty;
        return text.Length > 0 && TId.TryFrom(text, out var id) ? id : throw TypedIdJson.Refused<TId>();
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.Value);
    }
}
