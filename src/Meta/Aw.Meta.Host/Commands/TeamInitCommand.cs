using Aw.Meta.Core.Config;
using Aw.Meta.Features.Team;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'team-init' command.</summary>
internal static class TeamInitCommand
{
    internal static CommandEntry Entry { get; } = new(
        "team-init",
        new CommandSpec(
            "team-init --team <name> --repo <dir> --lanes a,b,c [--meta-lane meta] [--log <file>] [--force] [--json]",
            ["team", "repo", "lanes"],
            ["meta-lane", "log"],
            ["force"]),
        NeedsConfig: false,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var line = context.Line;
        var lanes = line.Value("lanes").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var log = line["log"] is { } file ? Path.GetFullPath(file) : null;
        var request = new TeamInitRequest(line.Value("team"), Path.GetFullPath(line.Value("repo")), lanes, line["meta-lane"] ?? "meta", log, line.Has("force"));
        return CommandOutput.Call<TeamInitRequest, TeamConfig>(context, request, static config => "wrote the team config for " + TeamShowCommand.Summary(config));
    }
}
