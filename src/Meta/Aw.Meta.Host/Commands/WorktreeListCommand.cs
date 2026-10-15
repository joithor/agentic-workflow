using System.Globalization;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Features.WorktreeList;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'worktree-list' command: every worktree of the team repo with its verdict. Exit 0.</summary>
internal static class WorktreeListCommand
{
    internal static CommandEntry Entry { get; } = new(
        "worktree-list",
        new CommandSpec("worktree-list [--no-fetch] [--json]", [], [], ["no-fetch"]),
        NeedsConfig: true,
        static context => CommandOutput.Call<ListWorktreesRequest, ListWorktreesResponse>(context, new ListWorktreesRequest(!context.Line.Has("no-fetch")), Text));

    private static string Text(ListWorktreesResponse response)
    {
        var width = response.Worktrees.Select(static w => w.Path.Length).DefaultIfEmpty().Max();
        var removable = response.Worktrees.Count(static w => w.Decision == WorktreeDecision.Removable);
        return string.Join(
            Environment.NewLine,
            [
                .. response.Worktrees.SelectMany(w => WorktreeText.Entry(w, width)),
                string.Create(CultureInfo.InvariantCulture, $"{response.Worktrees.Count} worktree(s): {removable} removable, {response.Worktrees.Count - removable} kept"),
            ]);
    }
}
