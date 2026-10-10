using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>A <see cref="JsonNode"/> written as 'JSON.stringify(JSON.parse(raw))' (see <see cref="NodeJsonValue"/>).</summary>
internal sealed class NodeJsonNodeConverter : JsonConverter<JsonNode>
{
    public override JsonNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonNode.Parse(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonNode value, JsonSerializerOptions options)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var raw = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            value.WriteTo(raw);
        }

        NodeJsonValue.WriteRestringified(writer, buffer.WrittenSpan);
    }
}
