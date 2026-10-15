using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Reads and writes a <see cref="FactType"/> as its wire word; a word this build doesn't know (or a non-string) reads as payload.</summary>
internal sealed class FactTypeJsonConverter : JsonConverter<FactType>
{
    private static readonly (FactType Value, string Name)[] _names =
    [
        (FactType.Text, "text"), (FactType.File, "file"), (FactType.Item, "item"), (FactType.Link, "link"),
        (FactType.ScopeMap, "scope-map"), (FactType.Packet, "packet"), (FactType.Evidence, "evidence"),
        (FactType.Decision, "decision"), (FactType.Payload, "payload"),
    ];

    public override FactType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? name = null;
        if (reader.TokenType == JsonTokenType.String)
        {
            name = reader.GetString();
        }
        else if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        foreach (var (value, text) in _names)
        {
            if (string.Equals(text, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return FactType.Payload;
    }

    public override void Write(Utf8JsonWriter writer, FactType value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var (candidate, text) in _names)
        {
            if (candidate == value)
            {
                writer.WriteStringValue(text);
                return;
            }
        }

        writer.WriteStringValue("payload");
    }
}
