using Aw.Meta.Features.WorktreeSweep;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'worktree-sweep' command: removes the removable worktrees. Exit 0, or 1 when git refused a removal.</summary>
internal static class WorktreeSweepCommand
{
    internal static CommandEntry Entry { get; } = new(
        "worktree-sweep",
        new CommandSpec("worktree-sweep [--dry-run] [--pr <n>] [--json]", [], ["pr"], ["dry-run"]),
        NeedsConfig: true,
        Run);

    private static Task<int> Run(CommandContext context)
    {
        int? pr = null;
        if (context.Line["pr"] is { } text)
        {
            if (!PrArgument.TryParse(text, out var number))
            {
                return Task.FromResult(PrArgument.Usage(context, Entry));
            }

            pr = number;
        }

        return CommandOutput.Call<SweepWorktreesRequest, SweepWorktreesResponse>(context, new SweepWorktreesRequest(context.Line.Has("dry-run"), pr), static _ => string.Empty, static r => r.Failed > 0);
    }
}
