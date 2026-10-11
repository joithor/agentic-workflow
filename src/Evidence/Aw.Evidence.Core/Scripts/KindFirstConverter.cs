using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Evidence.Core.Scripts;

/// <summary>
/// Reads an <see cref="ExpectedState"/> whatever its key order: a planner may write 'kind' anywhere in the object, but
/// System.Text.Json reads a polymorphic discriminator only as the first key (the shared AwJson profile keeps that
/// default). The object is copied with 'kind' moved to the front, then read by the ordinary polymorphic contract. A
/// missing or unknown kind is refused first, naming the known kinds; every other rule (required members) still applies,
/// and a failure keeps its place inside the object. Writing is unchanged ('kind' first).
/// </summary>
public sealed class KindFirstConverter : JsonConverter<ExpectedState>
{
    /// <inheritdoc/>
    public override ExpectedState? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var contract = Contract(options);
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize(ref reader, contract);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        CheckKind(document.RootElement, contract);
        try
        {
            return JsonSerializer.Deserialize(KindFirst(document.RootElement), contract);
        }
        catch (JsonException ex)
        {
            throw JsonInputProblem.Nested(ex);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ExpectedState value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, Contract(options));

    // A missing or unknown kind is named here, with the kinds the contract knows, before the polymorphic read.
    private static void CheckKind(JsonElement state, JsonTypeInfo<ExpectedState> contract)
    {
        if (!state.TryGetProperty("kind", out var kind))
        {
            throw JsonInputProblem.At("$", "missing required property \"kind\"");
        }

        var known = contract.PolymorphismOptions?.DerivedTypes.Select(static d => d.TypeDiscriminator?.ToString()).OfType<string>().ToList() ?? [];
        if (kind.ValueKind != JsonValueKind.String || !known.Contains(kind.GetString(), StringComparer.Ordinal))
        {
            throw JsonInputProblem.At("$.kind", $"{kind.GetRawText()} is not a known kind ({string.Join(", ", known)})");
        }
    }

    private static JsonTypeInfo<ExpectedState> Contract(JsonSerializerOptions options) =>
        (JsonTypeInfo<ExpectedState>)options.GetTypeInfo(typeof(ExpectedState));

    private static ReadOnlySpan<byte> KindFirst(JsonElement state)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var key in state.EnumerateObject().Where(static p => p.NameEquals("kind")))
            {
                key.WriteTo(writer);
            }

            foreach (var key in state.EnumerateObject().Where(static p => !p.NameEquals("kind")))
            {
                key.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan;
    }
}
