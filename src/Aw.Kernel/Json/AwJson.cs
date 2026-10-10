using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Kernel.Json;

/// <summary>
/// The one JSON profile every C# package uses for its state files, ledgers and CLI '--json' output.
/// <list type="bullet">
/// <item>camelCase property names in declaration order; dictionary keys are written as they are;</item>
/// <item>readable text: the relaxed encoder writes non-ASCII and HTML characters as they are, and escapes only what
/// JSON requires (quotes, backslashes, control characters). The output is never embedded in HTML;</item>
/// <item>numbers as .NET writes them (decimal keeps its scale, long its exact digits); NaN and the infinities are
/// not valid JSON and fail to write;</item>
/// <item>timestamps are UTC ISO-8601 with seven fractional digits and 'Z' (round-trip exact). A DateTimeOffset
/// is converted to UTC; a DateTime must be UTC (writing a local or unspecified one is a bug and throws). Reading
/// requires an ISO-8601 timestamp with 'Z' or an offset and returns UTC;</item>
/// <item>null properties are written, so a file always shows its whole shape;</item>
/// <item>indented output uses two spaces and '\n' on every OS.</item>
/// </list>
/// Reading is strict where typed ids and value objects depend on it: a missing positional (constructor) parameter
/// or 'required' member, or a null for a non-nullable one, is AW-KERNEL-001 (RespectRequiredConstructorParameters,
/// RespectNullableAnnotations). Not checked: a missing init-only member without 'required' keeps its initializer,
/// and collection elements may be null, so JSON shapes are positional records or mark init members 'required'.
/// Serialization is source-generated only (NativeAOT-safe): pass a package's <see cref="JsonSerializerContext"/>.
/// </summary>
public static class AwJson
{
    /// <summary>Builds the read-only options for <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The package's source-generated context.</param>
    /// <param name="indented">Two-space indentation with '\n' line breaks.</param>
    /// <returns>The options.</returns>
    public static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver resolver, bool indented = false)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            WriteIndented = indented,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
        };
        options.Converters.Add(new UtcTimestampConverter());
        options.Converters.Add(new UtcDateTimeConverter());
        options.MakeReadOnly();
        return options;
    }

    /// <summary>Writes <paramref name="value"/> as UTF-8 JSON.</summary>
    /// <param name="value">The value.</param>
    /// <param name="options">Options from <see cref="CreateOptions"/>.</param>
    /// <typeparam name="T">A type the options' resolver knows.</typeparam>
    /// <returns>The UTF-8 bytes.</returns>
    public static byte[] Serialize<T>(T value, JsonSerializerOptions options) =>
        JsonSerializer.SerializeToUtf8Bytes(value, TypeInfo<T>(options));

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
