// R9 hybrid id types, shared with the VogenDefaultProbe project (which must fail to build).
using System.Text.Json;
using System.Text.Json.Serialization;
using Vogen;

// Safe defaults for every value object in the assembly. Source generators can't see each other's output, so
// Vogen-generated STJ converters (invisible to the STJ generator: ids serialize as {}) and TryParse/IParsable
// (invisible to the request-delegate generator: route ids fall back to JSON-body binding, a 500) are both off,
// and each id declares its converter and TryParse in user code below.
[assembly: VogenDefaults(conversions: Conversions.None,
    parsableForStrings: ParsableForStrings.GenerateNothing, parsableForPrimitives: ParsableForPrimitives.GenerateNothing)]

namespace Spike.R9
{
    [ValueObject<string>]
    [JsonConverter(typeof(ItemIdJsonConverter))]
    public readonly partial struct ItemId : IParsable<ItemId>
    {
        private static Validation Validate(string value) =>
            string.IsNullOrWhiteSpace(value) ? Validation.Invalid("ItemId must not be empty") : Validation.Ok;

        // Route and query binding (RDG): false -> 400, never an exception.
        public static bool TryParse(string? s, IFormatProvider? provider, out ItemId result) => TryFrom(s ?? "", out result);
        public static ItemId Parse(string s, IFormatProvider? provider) =>
            TryParse(s, provider, out var id) ? id : throw new FormatException("ItemId must not be empty");
    }

    /// Invalid JSON values become JsonException (a 400 at an HTTP boundary), never Vogen's validation exception.
    public sealed class ItemIdJsonConverter : JsonConverter<ItemId>
    {
        public override ItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String && ItemId.TryFrom(reader.GetString() ?? "", out var id)
                ? id
                : throw new JsonException("ItemId must be a non-empty string");
        public override void Write(Utf8JsonWriter writer, ItemId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }

    [ValueObject<int>]
    [JsonConverter(typeof(TokenBudgetJsonConverter))]
    public readonly partial struct TokenBudget : IParsable<TokenBudget>
    {
        private static Validation Validate(int value) =>
            value < 0 ? Validation.Invalid("TokenBudget must be >= 0") : Validation.Ok;

        // VOG009 bans `result = default`, so an unparsable string goes through TryFrom with a value the
        // validator rejects (-1), which assigns `result` and returns false.
        public static bool TryParse(string? s, IFormatProvider? provider, out TokenBudget result) =>
            TryFrom(int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : -1, out result);
        public static TokenBudget Parse(string s, IFormatProvider? provider) =>
            TryParse(s, provider, out var b) ? b : throw new FormatException("TokenBudget must be an integer >= 0");
    }

    public sealed class TokenBudgetJsonConverter : JsonConverter<TokenBudget>
    {
        public override TokenBudget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n) && TokenBudget.TryFrom(n, out var b)
                ? b
                : throw new JsonException("TokenBudget must be an integer >= 0");
        public override void Write(Utf8JsonWriter writer, TokenBudget value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
    }

    public sealed record Payload(ItemId Id, TokenBudget Budget, string Name);

    // Respect* close the gaps the converter can't see: a missing constructor property would otherwise
    // deserialize as default(ItemId) without ever calling the converter.
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
    [JsonSerializable(typeof(Payload))]
    public sealed partial class SpikeJson : JsonSerializerContext;

    // Counterfactual: STJ defaults (no Respect*), to show what the options close.
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Payload))]
    public sealed partial class LaxJson : JsonSerializerContext;
}
