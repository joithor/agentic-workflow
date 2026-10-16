using System.Text.Json;
using Aw.Meta.Core;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// Picks the team a run acts on, and with it the state directory. A selected team (--team or AW_META_TEAM) uses teams/&lt;name&gt;; the
/// legacy flat layout keeps working when no team is selected, or when the selected name is the legacy config's own team. With nothing
/// selected and no legacy config, one team on disk is used and several are ambiguous. Nothing here writes.
/// </summary>
internal static class TeamResolver
{
    private const string Teams = "teams";
    private const string ConfigFile = "team.json";

    internal static (string StateDirectory, TeamLayout Layout) Resolve(string metaRoot, string? selected)
    {
        var legacyConfig = Path.Combine(metaRoot, ConfigFile);
        if (!string.IsNullOrEmpty(selected))
        {
            return Selected(metaRoot, selected, legacyConfig);
        }

        if (File.Exists(legacyConfig))
        {
            return (metaRoot, new TeamLayout(metaRoot, LegacyTeam(legacyConfig), Legacy: true, Ambiguous: []));
        }

        var found = Named(metaRoot);
        return found.Count switch
        {
            0 => (metaRoot, new TeamLayout(metaRoot, Team: null, Legacy: true, Ambiguous: [])),
            1 => (Path.Combine(metaRoot, Teams, found[0]), new TeamLayout(metaRoot, found[0], Legacy: false, Ambiguous: [])),
            _ => (metaRoot, new TeamLayout(metaRoot, Team: null, Legacy: true, Ambiguous: found)),
        };
    }

    /// <summary>The names of the teams that have a config under teams/, in order.</summary>
    /// <param name="metaRoot">The meta root.</param>
    /// <returns>The names.</returns>
    internal static IReadOnlyList<string> Named(string metaRoot)
    {
        var directory = Path.Combine(metaRoot, Teams);
        try
        {
            return Directory.Exists(directory)
                ? [.. Directory.EnumerateDirectories(directory)
                    .Where(d => File.Exists(Path.Combine(d, ConfigFile)) && TeamLayout.IsValidName(Path.GetFileName(d)))
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The team name the legacy flat config carries, or null when it can't be read.</summary>
    /// <param name="legacyConfig">The legacy team.json.</param>
    /// <returns>The name.</returns>
    internal static string? LegacyTeam(string legacyConfig)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(legacyConfig));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("team", out var team)
                && team.ValueKind == JsonValueKind.String ? team.GetString() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static (string StateDirectory, TeamLayout Layout) Selected(string metaRoot, string selected, string legacyConfig)
    {
        if (!TeamLayout.IsValidName(selected))
        {
            return (metaRoot, new TeamLayout(metaRoot, Team: null, Legacy: true, Ambiguous: [], Problem: $"team '{selected}' is not a valid name: use lowercase letters, digits and '-', starting with a letter or digit"));
        }

        var named = Path.Combine(metaRoot, Teams, selected);
        return !Directory.Exists(named) && File.Exists(legacyConfig) && LegacyTeam(legacyConfig) == selected
            ? (metaRoot, new TeamLayout(metaRoot, selected, Legacy: true, Ambiguous: []))
            : (named, new TeamLayout(metaRoot, selected, Legacy: false, Ambiguous: []));
    }
}
