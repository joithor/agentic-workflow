using Aw.Meta.Features.Lanes;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'lanes' command: refreshes the map, then prints it, or with --announce the text to send every lane.</summary>
internal static class LanesCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lanes",
        new CommandSpec("lanes [--announce] [--json]", [], [], ["announce"]),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var announce = context.Line.Has("announce");
        return CommandOutput.Call<RefreshLanesRequest, LanesResponse>(context, new RefreshLanesRequest(), map => announce ? map.Announcement : Table(map));
    }

    private static string Table(LanesResponse map) =>
        string.Join(
            Environment.NewLine,
            [
                map.Announcement.Split(':')[0],
                .. map.Entries.Select(e => $"{e.Lane,-12} {e.SessionName} ({e.Source}){Standing(map, e.Lane)}"),
                .. map.Unmapped.Select(lane => $"{lane,-12} (no session){Standing(map, lane)}"),
            ]);

    // A non-Claude lane's standing for the meta's obligations ("taking part", "launch-only: no obligations", ...); nothing for a Claude lane.
    private static string Standing(LanesResponse map, string lane) =>
        map.Standing?.FirstOrDefault(s => string.Equals(s.Lane, lane, StringComparison.OrdinalIgnoreCase)) is { } found ? $" [{found.Agent}: {found.State}]" : string.Empty;
}
