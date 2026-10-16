using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>Reads and writes a <see cref="ProducedFact"/>: the bare key as a string, a rename as '{ fact, from }'.</summary>
public sealed class ProducedFactJsonConverter : JsonConverter<ProducedFact>
{
    /// <inheritdoc/>
    public override ProducedFact Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && FactKey.TryFrom(reader.GetString() ?? string.Empty, out var key))
        {
            return new ProducedFact(key, From: null);
        }

        throw new JsonException("A produces entry is a fact key, or { fact, from }");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ProducedFact value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.Fact.Value);
    }
}
