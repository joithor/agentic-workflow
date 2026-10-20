using Aw.Kernel;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Infrastructure.Worktrees;

/// <summary>Worktrees through git. Never fetches, forces or resets.</summary>
internal sealed class GitWorktrees : IWorktrees
{
    public ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
}
