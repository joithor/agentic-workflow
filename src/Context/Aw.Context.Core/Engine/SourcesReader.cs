using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Context.Core.Onyx;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// Reads 'context.sources' for <see cref="SettingsReader"/> (spec §8.8): 'onyx' through <see cref="OnyxSettings.Create"/>
/// (SND-CONTEXT-055 or 002 with the key path) and 'linear.teams'. The shape (a map, known keys only) is SND-CONTEXT-001
/// naming the full key path. Linear's own token and url arrive with its native adapter (stage S-6).
/// </summary>
internal static partial class SourcesReader
{
    private static readonly string[] _sources = ["onyx", "linear"];
    private static readonly string[] _onyx = ["url", "token", "version", "managed"];
    private static readonly string[] _linear = ["teams"];

    /// <summary>The first problem with the shape of <paramref name="context"/>'s 'sources' map, or null.</summary>
    /// <param name="context">The whole 'context:' section (a map).</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Shape(JsonElement context)
    {
        if (!context.TryGetProperty("sources", out var sources) || sources.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (sources.ValueKind != JsonValueKind.Object)
        {
            return SettingsReader.Invalid("context.sources", "must be a map of sources (onyx, linear)");
        }

        return Unknown(sources, _sources) is { } name
            ? SettingsReader.Invalid("context.sources." + name, "is not a source this build knows (onyx, linear)")
            : Entry(sources, "onyx", _onyx) ?? Entry(sources, "linear", _linear);
    }

    /// <summary>The checked onyx settings (or null) and Linear teams of a well-shaped 'sources' map.</summary>
    /// <param name="section">The section as read, or null.</param>
    /// <returns>The values, or SND-CONTEXT-001, 002 or 055 naming the key path.</returns>
    internal static Result<(OnyxSettings? Onyx, IReadOnlyList<string> Teams)> Check(SourcesSection? section)
    {
        var teams = section?.Linear?.Teams ?? [];
        if (teams.FirstOrDefault(t => !TeamKey.IsMatch(t)) is { } bad)
        {
            return SettingsReader.Invalid("context.sources.linear.teams",
                $"'{SettingsReader.Printable(bad)}' isn't a Linear team key (a capital letter, then one or more capitals or digits, e.g. ENG)");
        }

        if (section?.Onyx is not { } onyx)
        {
            return (null, teams);
        }

        var created = OnyxSettings.Create(onyx.Url, onyx.Token, onyx.Version, onyx.Managed ?? false);
        return created.IsError ? created.Error : (created.Value, teams);
    }

    private static Error? Entry(JsonElement sources, string name, string[] known)
    {
        if (!sources.TryGetProperty(name, out var entry) || entry.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var keys = string.Join(", ", known);
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return SettingsReader.Invalid($"context.sources.{name}", $"must be a map ({keys})");
        }

        return Unknown(entry, known) is { } key
            ? SettingsReader.Invalid($"context.sources.{name}.{key}", $"is not a context.sources.{name} setting this build knows ({keys})")
            : null;
    }

    private static string? Unknown(JsonElement map, string[] known) =>
        map.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !known.Contains(n, StringComparer.Ordinal)) is { } name
            ? SettingsReader.Printable(name)
            : null;

    [GeneratedRegex("^" + OnyxRecordKeys.TeamKeyPattern + "\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TeamKey { get; }
}
