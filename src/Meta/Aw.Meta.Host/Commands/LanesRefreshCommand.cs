using Aw.Meta.Features.Lanes;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'lanes-refresh' command.</summary>
internal static class LanesRefreshCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lanes-refresh",
        CommandSpec.None("lanes-refresh [--json]"),
        NeedsConfig: true,
        static context => CommandOutput.Call<RefreshLanesRequest, LanesResponse>(context, new RefreshLanesRequest(), Text));

    private static string Text(LanesResponse map) =>
        $"{map.Announcement} ({(map.Changed ? "changed" : "unchanged")})";
}
