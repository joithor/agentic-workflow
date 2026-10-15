using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Reads and writes <see cref="FactType"/> as its kebab-case name; a name it doesn't know reads as <see cref="FactType.Payload"/> (API spec section 3.8).</summary>
public sealed class FactTypeJsonConverter : JsonConverter<FactType>
{
    private static readonly (FactType Value, string Name)[] _names =
    [
        (FactType.Text, "text"), (FactType.File, "file"), (FactType.Item, "item"), (FactType.Link, "link"),
        (FactType.ScopeMap, "scope-map"), (FactType.Packet, "packet"), (FactType.Evidence, "evidence"),
        (FactType.Decision, "decision"), (FactType.Payload, "payload"),
    ];

    /// <inheritdoc />
    public override FactType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        foreach (var (value, text) in _names)
        {
            if (string.Equals(text, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return FactType.Payload;
    }

    /// <inheritdoc />
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
