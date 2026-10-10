using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Kernel.Json;

/// <summary>
/// The one JSON profile every C# package uses to read and write files the TS packages share (spec §4c, A1): the
/// bytes match what Node's 'JSON.stringify' writes, for the shapes listed here.
/// <list type="bullet">
/// <item>camelCase property names, in declaration order (which reproduces key insertion order);</item>
/// <item>Node's escaping (only quotes, backslashes, control characters and lone surrogates);</item>
/// <item>numbers as Node holds them: double, float, decimal, long and ulong are written as the nearest double, laid out
/// like Number#toString; NaN and the infinities as null, and null reads back as NaN for double and float. int and
/// smaller integers are exact in a double and need no converter;</item>
/// <item>DateTimeOffset and UTC DateTime like Date#toISOString (UTC, milliseconds, Z), and only that form is read
/// back; writing a local or unspecified DateTime is a bug and throws;</item>
/// <item>null properties are left out, as TS leaves out undefined optionals. A property that TS writes as an
/// explicit null opts in with '[JsonIgnore(Condition = JsonIgnoreCondition.Never)]';</item>
/// <item>JsonElement and JsonNode values are written as 'JSON.stringify(JSON.parse(raw))' (see <see cref="Restringify"/>).
/// A JsonNode can't hold a lone surrogate (it unescapes strings itself), so writing one that came from such input
/// throws; use JsonElement for untyped data that may contain them;</item>
/// <item>indented output is exactly 'JSON.stringify(value, null, 2)': two spaces and '\n' on every OS.</item>
/// </list>
/// Reading: a missing positional (constructor) parameter or 'required' member, or a null for a non-nullable one,
/// is AW-KERNEL-001 (RespectRequiredConstructorParameters, RespectNullableAnnotations). Not checked: a missing
/// init-only member without 'required' keeps its initializer, and collection elements may be null. JSON shapes are
/// therefore positional records or mark their init members 'required'.
/// <para>
/// Serialization is source-generated only (no reflection, so it is NativeAOT-safe): pass a package's
/// <see cref="JsonSerializerContext"/> as the resolver. Not reproduced, by design of the shapes rather than in code:
/// a derived record writes its own members before its base's (JS writes base fields first), so serialized shapes
/// are sealed records without a base, or the base members carry '[JsonPropertyOrder(-1)]'; and JS orders
/// array-index keys first, so a dictionary with such keys must be built in that order. A dictionary key with a lone
/// surrogate throws a JsonException rather than silently writing U+FFFD.
/// </para>
/// </summary>
public static class NodeJson
{
    /// <summary>Builds the read-only options for <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The package's source-generated context.</param>
    /// <returns>The options.</returns>
    public static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver resolver)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = NodeJsonEncoder.Instance,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new NodeStringConverter());
        options.Converters.Add(new JsNumberConverter<double>());
        options.Converters.Add(new JsNumberConverter<float>());
        options.Converters.Add(new JsNumberConverter<decimal>());
        options.Converters.Add(new JsNumberConverter<long>());
        options.Converters.Add(new JsNumberConverter<ulong>());
        options.Converters.Add(new IsoTimestampConverter());
        options.Converters.Add(new IsoDateTimeConverter());
        options.Converters.Add(new NodeJsonElementConverter());
        options.Converters.Add(new NodeJsonNodeConverter());
        options.MakeReadOnly();
        return options;
    }

    /// <summary>Writes <paramref name="value"/> as UTF-8 JSON.</summary>
    /// <param name="value">The value.</param>
    /// <param name="options">Options from <see cref="CreateOptions"/>.</param>
    /// <param name="indented">Lay it out as 'JSON.stringify(value, null, 2)' does.</param>
    /// <typeparam name="T">A type the options' resolver knows.</typeparam>
    /// <returns>The UTF-8 bytes.</returns>
    public static byte[] Serialize<T>(T value, JsonSerializerOptions options, bool indented = false)
    {
        var compact = JsonSerializer.SerializeToUtf8Bytes(value, TypeInfo<T>(options));
        return indented ? NodeJsonIndenter.Indent(compact) : compact;
    }

    /// <summary>
    /// 'JSON.stringify(JSON.parse(text))' for untyped JSON, byte for byte: what judge's input_digest hashes (spec §4c).
    /// Numbers are re-laid out as doubles, array-index keys move first, a duplicate key keeps its first position and
    /// its last value, and strings are re-escaped the Node way (lone surrogates included, in keys too).
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON text.</param>
    /// <param name="indented">Lay it out as 'JSON.stringify(value, null, 2)' does.</param>
    /// <returns>The UTF-8 bytes, or AW-KERNEL-001 when JSON.parse would reject the text.</returns>
    public static Result<byte[]> Restringify(ReadOnlySpan<byte> utf8Json, bool indented = false) =>
        NodeJsonValue.Restringify(utf8Json).Map(text =>
        {
            var compact = Encoding.UTF8.GetBytes(text);
            return indented ? NodeJsonIndenter.Indent(compact) : compact;
        });

    /// <summary>Reads UTF-8 JSON. Malformed JSON, a wrong shape or a null document is an expected error, not an exception.</summary>
    /// <param name="utf8Json">The UTF-8 bytes.</param>
    /// <param name="options">Options from <see cref="CreateOptions"/>.</param>
    /// <typeparam name="T">A type the options' resolver knows.</typeparam>
    /// <returns>The value, or AW-KERNEL-001 saying what is wrong.</returns>
    public static Result<T> Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options)
    {
        try
        {
            var value = JsonSerializer.Deserialize(utf8Json, TypeInfo<T>(options));
            return value is null ? KernelErrors.Invalid.With("The JSON document is null.") : value;
        }
        catch (JsonException ex)
        {
            return KernelErrors.Invalid.With($"Invalid JSON for {typeof(T).Name}: {ex.Message}");
        }
    }

    private static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions options) =>
        (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
