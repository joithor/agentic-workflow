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
            [map.Announcement.Split(':')[0], .. map.Entries.Select(e => $"{e.Lane,-12} {e.SessionName} ({e.Source})"), .. map.Unmapped.Select(lane => $"{lane,-12} (no session)")]);
}
