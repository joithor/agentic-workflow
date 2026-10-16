using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>Reads and writes a <see cref="ProducedFact"/>: the bare key as a string, a rename as '{ fact, from }' (both required).</summary>
public sealed class ProducedFactJsonConverter : JsonConverter<ProducedFact>
{
    private const string Expected = "A produces entry is a fact key, or { fact, from }";

    /// <inheritdoc/>
    public override ProducedFact Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.String)
        {
            return new ProducedFact(Key(root), From: null);
        }

        // why: exactly the two fields, each once (a repeated property would collapse in the dictionary)
        var fields = root.ValueKind == JsonValueKind.Object ? root.EnumerateObject().ToList() : [];
        return fields.Count == 2 && fields.Select(f => f.Name).Order(StringComparer.Ordinal).SequenceEqual(["fact", "from"], StringComparer.Ordinal)
            ? new ProducedFact(Key(fields.Single(f => f.Name == "fact").Value), Key(fields.Single(f => f.Name == "from").Value))
            : throw new JsonException(Expected);
    }

    private static FactKey Key(JsonElement element) =>
        element.ValueKind == JsonValueKind.String && FactKey.TryFrom(element.GetString() ?? string.Empty, out var key) ? key : throw new JsonException(Expected);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ProducedFact value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (value.From is not { } from || from == value.Fact)
        {
            writer.WriteStringValue(value.Fact.Value);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("fact", value.Fact.Value);
        writer.WriteString("from", from.Value);
        writer.WriteEndObject();
    }
}
