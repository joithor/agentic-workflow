using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Sqlite;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ITeamCatalog over the meta root: teams/&lt;name&gt;/ for named teams, the flat files for the legacy one. Migration renames the
/// legacy files into teams/&lt;its team name&gt; and leaves gates/ where it is (gate trees are registered with git by absolute path, and
/// the gate records in meta.db point at their step logs).
/// </summary>
/// <param name="layout">Which team this run acts on, and the meta root.</param>
/// <param name="sessions">The live Claude sessions, to tell which team has a live meta session.</param>
/// <param name="json">The AwJson options of the host's context.</param>
/// <param name="clock">The clock the databases are opened with.</param>
internal sealed class FileTeamCatalog(TeamLayout layout, ISessionDirectory sessions, JsonSerializerOptions json, TimeProvider clock) : ITeamCatalog
{
    private const string GatesDirectory = "gates";

    private readonly string _teamsDirectory = Path.Combine(layout.MetaRoot, "teams");
    private readonly string _legacyConfig = Path.Combine(layout.MetaRoot, "team.json");

    public async ValueTask<Result<IReadOnlyList<TeamSummary>>> List(CancellationToken cancellationToken)
    {
        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var running = live.IsSuccess ? live.Value : [];
        var teams = new List<TeamSummary>();
        foreach (var name in TeamResolver.Named(layout.MetaRoot))
        {
            var directory = Path.Combine(_teamsDirectory, name);
            teams.Add(await Summarize(new TeamSource(name, "named", directory, name == layout.Team && !layout.Legacy), running, cancellationToken).ConfigureAwait(false));
        }

        if (File.Exists(_legacyConfig))
        {
            var name = TeamResolver.LegacyTeam(_legacyConfig) ?? "(unnamed)";
            if (teams.TrueForAll(t => t.Name != name))
            {
                teams.Add(await Summarize(new TeamSource(name, "legacy", layout.MetaRoot, layout.Legacy), running, cancellationToken).ConfigureAwait(false));
            }
        }

        return teams.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    public ValueTask<Result<TeamMigration>> Migrate(bool dryRun, CancellationToken cancellationToken)
    {
        if (!File.Exists(_legacyConfig))
        {
            return ValueTask.FromResult<Result<TeamMigration>>(MetaErrors.NoTeamConfig.With($"no legacy team config at {_legacyConfig}; nothing to migrate"));
        }

        var name = TeamResolver.LegacyTeam(_legacyConfig);
        if (name is null || !TeamLayout.IsValidName(name))
        {
            return ValueTask.FromResult<Result<TeamMigration>>(MetaErrors.TeamMigrationRefused.With($"{_legacyConfig} has no team name usable as a directory (lowercase letters, digits and '-')"));
        }

        var target = Path.Combine(_teamsDirectory, name);
        if (Directory.Exists(target) || File.Exists(target))
        {
            return ValueTask.FromResult<Result<TeamMigration>>(MetaErrors.TeamMigrationRefused.With($"{target} already exists; move or remove it first"));
        }

        var entries = Directory.EnumerateFileSystemEntries(layout.MetaRoot).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();
        var moved = entries.Where(e => e is not ("teams" or GatesDirectory)).ToList();
        var left = entries.Where(e => e == GatesDirectory).Select(e => e + " (gate trees are registered with git by absolute path; sweep them with worktree-sweep, they are throwaway)").ToList();
        var plan = new TeamMigration(name, layout.MetaRoot, target, moved, left, dryRun);
        return ValueTask.FromResult(dryRun ? plan : Move(plan));
    }

    private static Result<TeamMigration> Move(TeamMigration plan)
    {
        var done = new List<string>();
        try
        {
            _ = Directory.CreateDirectory(plan.To);
            foreach (var entry in plan.Moved)
            {
                Relocate(Path.Combine(plan.From, entry), Path.Combine(plan.To, entry));
                done.Add(entry);
            }

            return plan;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            foreach (var entry in done)
            {
                Relocate(Path.Combine(plan.To, entry), Path.Combine(plan.From, entry));
            }

            return MetaErrors.TeamMigrationRefused.With($"moving into {plan.To} failed ({exception.Message}); the entries already moved were put back");
        }
    }

    private static void Relocate(string from, string to)
    {
        if (Directory.Exists(from))
        {
            Directory.Move(from, to);
        }
        else
        {
            File.Move(from, to);
        }
    }

    private sealed record TeamSource(string Name, string Kind, string Directory, bool Selected);

    private async ValueTask<TeamSummary> Summarize(TeamSource source, IReadOnlyList<Core.Lanes.LiveSession> running, CancellationToken cancellationToken)
    {
        var (name, kind, directory, selected) = source;
        var config = Read(Path.Combine(directory, "team.json"));
        var role = await Role(directory, cancellationToken).ConfigureAwait(false);
        var isLive = role is not null && running.Any(s => s.Session == role.Session || (role.Name.Length > 0 && s.Name == role.Name));
        return new TeamSummary(name, kind, config?.Repo ?? "(invalid config)", config?.Lanes ?? [], role is null ? null : role.Name.Length > 0 ? role.Name : role.Session.Value, isLive, selected);
    }

    private TeamConfig? Read(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var config = AwJson.Deserialize<TeamConfig>(bytes.AsSpan().StartsWith("﻿"u8) ? bytes.AsSpan(3) : bytes, json);
            return config.IsError ? null : config.Value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Only a database that is already there is opened: listing never creates a team's state.
    private async ValueTask<Core.Messages.MetaRole?> Role(string directory, CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(directory, MetaDatabase.FileName)))
        {
            return null;
        }

        var role = await new SqliteMessageStore(new MetaDatabase(MetaDatabase.OptionsFor(directory), clock)).Role(cancellationToken).ConfigureAwait(false);
        return role.IsSuccess ? role.Value : null;
    }
}
