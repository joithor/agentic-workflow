using Aw.Meta.Features.Team;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'team-list' command.</summary>
internal static class TeamListCommand
{
    internal static CommandEntry Entry { get; } = new(
        "team-list",
        CommandSpec.None("team-list [--json]"),
        NeedsConfig: false,
        static context => CommandOutput.Call<TeamListRequest, TeamListResponse>(context, new TeamListRequest(), Text));

    private static string Text(TeamListResponse response) =>
        response.Teams.Count == 0
            ? "no teams; run aw-meta team-init --team <name> --repo <dir> --lanes a,b"
            : string.Join(Environment.NewLine, response.Teams.Select(Line));

    private static string Line(Core.Ports.TeamSummary team) =>
        $"{(team.Selected ? "*" : " ")} {team.Name} [{team.Layout}]: repo {team.Repo}, lanes {string.Join(", ", team.Lanes)}, "
        + (team.MetaSession is null ? "no meta role" : $"meta {team.MetaSession} ({(team.Live ? "live" : "not running")})");
}
