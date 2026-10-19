using Aw.Meta.Core.Watch;
using Aw.Meta.Features.Usage;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'usage-wait' command: blocks until no lane or meta is limited, then prints the USAGE OK lines. Exit 0 when the window is back, 1 when the wait ran out.</summary>
internal static class UsageWaitCommand
{
    private static readonly TimeSpan _defaultMax = TimeSpan.FromHours(12);

    internal static CommandEntry Entry { get; } = new(
        "usage-wait",
        new CommandSpec("usage-wait [--until-ok] [--max 12h] [--json]", [], ["max"], ["until-ok"]),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        var max = _defaultMax;
        if (context.Line["max"] is { } text && !WatchDuration.TryParse(text, out max))
        {
            return Task.FromResult(CommandOutput.Usage(context.Io, "--max takes a duration such as 90m, 12h or 48h", Entry.Spec.Usage));
        }

        return CommandOutput.Call<UsageWaitRequest, UsageWaitResponse>(context, new UsageWaitRequest(context.Line.Has("until-ok"), max), static r => string.Join(Environment.NewLine, r.Lines), static r => !r.Ok);
    }
}
