using Aw.Meta.Core.Ports;
using Aw.Meta.Features.Team;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'team-migrate' command: the explicit, opt-in move of the legacy flat layout into teams/&lt;its team&gt;.</summary>
internal static class TeamMigrateCommand
{
    internal static CommandEntry Entry { get; } = new(
        "team-migrate",
        new CommandSpec("team-migrate [--dry-run] [--json]", [], [], ["dry-run"]),
        NeedsConfig: false,
        static context => CommandOutput.Call<TeamMigrateRequest, TeamMigration>(context, new TeamMigrateRequest(context.Line.Has("dry-run")), Text));

    private static string Text(TeamMigration migration) =>
        string.Join(
            Environment.NewLine,
            $"{(migration.DryRun ? "would move" : "moved")} {migration.Team}: {migration.From} -> {migration.To}",
            "entries: " + (migration.Moved.Count == 0 ? "(none)" : string.Join(", ", migration.Moved)),
            "left: " + (migration.Left.Count == 0 ? "(none)" : string.Join("; ", migration.Left)));
}
