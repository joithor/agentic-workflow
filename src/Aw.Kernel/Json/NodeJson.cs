using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Kernel.Json;

/// <summary>
/// The one JSON profile every C# package uses to read and write files the TS packages share (spec §4c, A1): the
/// bytes match what Node's 'JSON.stringify' writes.
/// <list type="bullet">
/// <item>camelCase property names, in record declaration order (which reproduces key insertion order);</item>
/// <item>Node's escaping (only quotes, backslashes, control characters and lone surrogates);</item>
/// <item>doubles laid out like Node's Number#toString; NaN and the infinities as null;</item>
/// <item>timestamps like Date#toISOString (UTC, milliseconds, Z), and only that form is read back;</item>
/// <item>null properties are left out, as TS leaves out undefined optionals. A property that TS writes as an
/// explicit null opts in with '[JsonIgnore(Condition = JsonIgnoreCondition.Never)]';</item>
/// <item>indented output is exactly 'JSON.stringify(value, null, 2)': two spaces and '\n' on every OS.</item>
/// </list>
/// Serialization is source-generated only (no reflection, so it is NativeAOT-safe): pass a package's
/// <see cref="JsonSerializerContext"/> as the resolver. Not reproduced: JS orders integer-like object keys first;
/// a dictionary with such keys must be built in that order. Property names with lone surrogates are written with
/// U+FFFD.
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
        };
        options.Converters.Add(new NodeStringConverter());
        options.Converters.Add(new NodeNumberConverter());
        options.Converters.Add(new IsoTimestampConverter());
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
