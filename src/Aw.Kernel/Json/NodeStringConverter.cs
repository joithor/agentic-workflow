using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>Writes and reads strings exactly as Node does, lone surrogates included (see <see cref="NodeJsonEscaping"/>).</summary>
internal sealed class NodeStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String
            ? ReadText(ref reader)
            : throw new JsonException($"Expected a JSON string, found {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteRawValue(NodeJsonEscaping.Quote(value), skipInputValidation: true);

    /// <summary>Reads a dictionary key. Writing one goes through NodeJsonEncoder (the writer escapes names itself).</summary>
    public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ReadText(ref reader);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value);

    private static string ReadText(ref Utf8JsonReader reader)
    {
        if (!reader.ValueIsEscaped)
        {
            return reader.GetString() ?? string.Empty;
        }

        // The reader's own unescaping rejects lone surrogates, which Node writes; unescape the raw token instead.
        var raw = reader.HasValueSequence ? Encoding.UTF8.GetString(reader.ValueSequence) : Encoding.UTF8.GetString(reader.ValueSpan);
        return NodeJsonEscaping.Unescape(raw);
    }
}
