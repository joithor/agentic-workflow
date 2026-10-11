using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Core;

/// <summary>Reads an input document through the AwJson profile and reports any failure under one catalogue code.</summary>
internal static class JsonInput
{
    internal static Result<T> Read<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options, ErrorDefinition error)
    {
        try
        {
            var parsed = AwJson.Deserialize<T>(utf8Json, options);
            return parsed.IsError ? error.With(parsed.Error.Message) : parsed;
        }
        catch (NotSupportedException ex)
        {
            // An abstract polymorphic record read without its leading 'kind' (or with an unknown one).
            return error.With($"Invalid JSON for {typeof(T).Name}: {ex.Message}");
        }
    }
}
