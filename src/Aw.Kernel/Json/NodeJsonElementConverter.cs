using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Kernel.Json;

/// <summary>A <see cref="JsonElement"/> written as 'JSON.stringify(JSON.parse(raw))' (see <see cref="NodeJsonValue"/>).</summary>
internal sealed class NodeJsonElementConverter : JsonConverter<JsonElement>
{
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonElement.ParseValue(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options) =>
        NodeJsonValue.WriteRestringified(writer, Encoding.UTF8.GetBytes(value.GetRawText()));
}
