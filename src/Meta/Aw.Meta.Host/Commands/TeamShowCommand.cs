using Aw.Meta.Core.Config;
using Aw.Meta.Features.Team;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'team-show' command.</summary>
internal static class TeamShowCommand
{
    internal static CommandEntry Entry { get; } = new(
        "team-show",
        CommandSpec.None("team-show [--json]"),
        NeedsConfig: true,
        static context => CommandOutput.Call<TeamShowRequest, TeamConfig>(context, new TeamShowRequest(), Text));

    internal static string Summary(TeamConfig config) =>
        $"{config.Team}: lanes {string.Join(", ", config.Lanes)} (meta lane {config.MetaLane}), repo {config.Repo}";

    private static string Text(TeamConfig config) =>
        string.Join(
            Environment.NewLine,
            Summary(config),
            "log: " + (config.Log ?? "(default)"),
            "gate scopes: " + (config.Gate.Scopes.Count == 0 ? "(none)" : string.Join(", ", config.Gate.Scopes.Keys)),
            "known flakes: " + (config.KnownFlakes.Count == 0 ? "(none)" : string.Join(", ", config.KnownFlakes)));
}
