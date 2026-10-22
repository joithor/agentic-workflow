using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Removes the phase worktree an ended phase instance owns (line spec LN-WF2). It never forces: a tree with uncommitted files is left as it is.</summary>
/// <param name="contents">Reads the stored 'fact:workspace'.</param>
/// <param name="worktrees">Removes the worktree.</param>
internal sealed class PhaseWorkspaceRemoval(IFactContents contents, IWorktrees worktrees)
{
    /// <summary>Removes the worktree <paramref name="workspace"/> names.</summary>
    /// <param name="workspace">The stored 'fact:workspace'.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>True when the worktree is gone (or was); false when it was left as it is.</returns>
    public ValueTask<bool> Remove(FactValue workspace, CancellationToken cancellationToken) => throw new NotSupportedException();
}
