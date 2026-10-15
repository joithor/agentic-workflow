using Aw.Meta.Features.WorktreeList;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'worktree-list' command: every worktree of the team repo with its verdict. Exit 0.</summary>
internal static class WorktreeListCommand
{
    internal static CommandEntry Entry { get; } = new(
        "worktree-list",
        new CommandSpec("worktree-list [--no-fetch] [--json]", [], [], ["no-fetch"]),
        NeedsConfig: true,
        static context => CommandOutput.Call<ListWorktreesRequest, ListWorktreesResponse>(context, new ListWorktreesRequest(!context.Line.Has("no-fetch")), static _ => string.Empty));
}
