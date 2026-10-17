using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Binds a sindri_* tool call's arguments (API spec §8). Every refusal is SND-CONTEXT-022 and names the fix; what an id
/// or text means is left to the service behind the tool.
/// </summary>
internal static class McpArguments
{
    /// <summary>Refuses any argument: the tool takes none.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <returns>The refusal, or null.</returns>
    internal static Error? None(string tool, IDictionary<string, JsonElement>? arguments) =>
        arguments is { Count: > 0 }
            ? ContextErrors.McpArgumentsInvalid.With($"{tool} takes no arguments, got '{string.Join("', '", arguments.Keys)}' (fix: call it with {{}})")
            : null;

    /// <summary>Refuses an argument that is not one of <paramref name="allowed"/>.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="allowed">The names the tool takes.</param>
    /// <returns>The refusal, or null.</returns>
    internal static Error? Only(string tool, IDictionary<string, JsonElement>? arguments, params string[] allowed) => throw new NotSupportedException();

    /// <summary>A required string argument.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="example">The value the example call shows.</param>
    /// <returns>The text, or the refusal.</returns>
    internal static Result<string> RequiredString(string tool, IDictionary<string, JsonElement>? arguments, string name, string example) => throw new NotSupportedException();

    /// <summary>An optional string argument; null counts as absent.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The text, null when absent, or the refusal.</returns>
    internal static Result<string?> OptionalString(string tool, IDictionary<string, JsonElement>? arguments, string name) => throw new NotSupportedException();

    /// <summary>An optional whole number of at least <paramref name="min"/>; null counts as absent.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="min">The least value.</param>
    /// <returns>The number, null when absent, or the refusal.</returns>
    internal static Result<long?> OptionalLong(string tool, IDictionary<string, JsonElement>? arguments, string name, long min = 0) => throw new NotSupportedException();

    /// <summary>A required enum argument, read by the names the JSON context gives its values.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="info">The enum's JSON type information.</param>
    /// <returns>The value, or the refusal listing the names.</returns>
    internal static Result<T> RequiredEnum<T>(string tool, IDictionary<string, JsonElement>? arguments, string name, JsonTypeInfo<T> info)
        where T : struct, Enum => throw new NotSupportedException();
}
