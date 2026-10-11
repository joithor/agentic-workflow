using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel;

namespace Aw.Evidence.Core;

/// <summary>
/// Reads an input document with the package's AwJson options and reports any failure under one catalogue code, as
/// "&lt;JSON path&gt;: &lt;problem&gt;" in the input's own terms (never a CLR type name). It reads through the options' type
/// info directly rather than AwJson.Deserialize, because the problem is built from the JsonException's path and
/// position, which AwJson.Deserialize folds into a message.
/// </summary>
internal static class JsonInput
{
    internal static Result<T> Read<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options, ErrorDefinition error)
    {
        try
        {
            var value = JsonSerializer.Deserialize(utf8Json, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
            return value is null ? error.With("$: must not be null") : value;
        }
        catch (JsonException ex)
        {
            return error.With(JsonInputProblem.Describe(ex, utf8Json));
        }
        catch (NotSupportedException)
        {
            // A polymorphic value read without a converter that checks its 'kind' first.
            return error.With("$: an object is missing its \"kind\"");
        }
    }
}
