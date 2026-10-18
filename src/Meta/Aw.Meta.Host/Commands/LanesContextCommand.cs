using System.Globalization;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LanesContext;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'lanes-context' command: each lane's context window against context.recycleAt. Exit 1 when a lane is due for a recycle.</summary>
internal static class LanesContextCommand
{
    internal static CommandEntry Entry { get; } = new(
        "lanes-context",
        CommandSpec.None("lanes-context [--json]"),
        NeedsConfig: true,
        static context => CommandOutput.Call<LanesContextRequest, LanesContextResponse>(context, new LanesContextRequest(), Text, static r => r.Attention));

    private static string Text(LanesContextResponse response) =>
        string.Join(
            Environment.NewLine,
            [
                "context: recycle at " + (response.RecycleAt / 1000).ToString(CultureInfo.InvariantCulture) + "k",
                .. response.Lanes.Select(Row),
            ]);

    private static string Row(LaneContext lane) =>
        $"{lane.Lane,-12} {lane.Compact(),6}  {lane.Verdict.ToString().ToUpperInvariant(),-8} session {lane.Session?.Value ?? "-"}  last activity {lane.LastActivity?.ToString("u", CultureInfo.InvariantCulture) ?? "-"}  {(lane.Transcript is { } path ? path : "transcript missing")}{(lane.Reason is { } reason ? " (" + reason + ")" : string.Empty)}";
}
