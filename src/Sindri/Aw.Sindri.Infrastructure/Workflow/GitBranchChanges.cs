using Aw.Kernel;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IBranchChanges"/> through git: the name-status of the diff, its zero-context patch for the added lines, and the log for the commits.</summary>
internal sealed class GitBranchChanges : IBranchChanges
{
    public ValueTask<Result<BranchChanges>> Between(string worktree, string baseSha, string head, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red");
}
