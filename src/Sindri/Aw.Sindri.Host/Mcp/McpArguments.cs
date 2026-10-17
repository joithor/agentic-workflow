using System.Globalization;
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
    internal static Error? Only(string tool, IDictionary<string, JsonElement>? arguments, params string[] allowed)
    {
        var unknown = (arguments?.Keys ?? []).FirstOrDefault(k => !allowed.Contains(k, StringComparer.Ordinal));
        return unknown is null
            ? null
            : ContextErrors.McpArgumentsInvalid.With($"{tool} has no argument '{unknown}' (fix: {string.Join(", ", allowed)} only)");
    }

    /// <summary>A required string argument.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="example">The value the example call shows.</param>
    /// <returns>The text, or the refusal.</returns>
    internal static Result<string> RequiredString(string tool, IDictionary<string, JsonElement>? arguments, string name, string example) =>
        arguments is not null && arguments.TryGetValue(name, out var raw) && raw.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(raw.GetString())
            ? raw.GetString()!   // why: a String kind always has a string
            : ContextErrors.McpArgumentsInvalid.With($"{tool} needs '{name}', a non-blank string (fix: call it with {{\"{name}\": \"{example}\"}})");

    /// <summary>An optional string argument; null counts as absent.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The text, null when absent, or the refusal.</returns>
    internal static Result<string?> OptionalString(string tool, IDictionary<string, JsonElement>? arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return (string?)null;
        }

        return raw.ValueKind == JsonValueKind.String
            ? raw.GetString()
            : ContextErrors.McpArgumentsInvalid.With($"'{name}' must be a string, got {Kind(raw)} (fix: see {tool}'s input schema)");
    }

    /// <summary>An optional whole number of at least <paramref name="min"/>; null counts as absent.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="min">The least value.</param>
    /// <returns>The number, null when absent, or the refusal.</returns>
    internal static Result<long?> OptionalLong(string tool, IDictionary<string, JsonElement>? arguments, string name, long min = 0)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return (long?)null;
        }

        return raw.ValueKind == JsonValueKind.Number && raw.TryGetInt64(out var number) && number >= min
            ? number
            : ContextErrors.McpArgumentsInvalid.With($"'{name}' must be a whole number of {min.ToString(CultureInfo.InvariantCulture)} or more, got {Kind(raw)} {raw.GetRawText()} (fix: see {tool}'s input schema)");
    }

    /// <summary>A required enum argument, read by the names the JSON context gives its values.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="info">The enum's JSON type information.</param>
    /// <returns>The value, or the refusal listing the names.</returns>
    internal static Result<T> RequiredEnum<T>(string tool, IDictionary<string, JsonElement>? arguments, string name, JsonTypeInfo<T> info)
        where T : struct, Enum
    {
        var names = Enum.GetValues<T>().Select(v => JsonSerializer.SerializeToElement(v, info).GetString() ?? string.Empty).ToArray();
        if (arguments is not null && arguments.TryGetValue(name, out var raw) && raw.ValueKind == JsonValueKind.String
            && Array.IndexOf(names, raw.GetString()) >= 0)
        {
            return JsonSerializer.Deserialize(raw, info);
        }

        return ContextErrors.McpArgumentsInvalid.With($"{tool} needs '{name}', one of {string.Join(", ", names)} (fix: call it with {{\"{name}\": \"{names[0]}\"}})");
    }

    private static string Kind(JsonElement value) => value.ValueKind.ToString().ToLowerInvariant();
}
