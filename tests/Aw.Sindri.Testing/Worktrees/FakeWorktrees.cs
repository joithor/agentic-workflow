using Aw.Kernel;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Testing.Worktrees;

/// <summary>The in-memory <see cref="IWorktrees"/>: repos with the commits they hold, and the worktrees made in them. Passes <see cref="WorktreesContract"/>.</summary>
public sealed class FakeWorktrees : IWorktrees
{
    /// <summary>Tells the fake which commits a repo holds; a repo or sha it was not told about fails like git does.</summary>
    /// <param name="repoRoot">The repo's path.</param>
    /// <param name="commits">Its commit shas.</param>
    /// <returns>This fake.</returns>
    public FakeWorktrees WithRepo(string repoRoot, params string[] commits) => throw new NotSupportedException();

    /// <summary>Leaves an uncommitted file in the worktree at <paramref name="path"/>.</summary>
    /// <param name="path">The worktree's path.</param>
    public void MakeDirty(string path) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
}
