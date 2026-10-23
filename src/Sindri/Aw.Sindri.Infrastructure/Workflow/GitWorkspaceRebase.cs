#pragma warning disable CS1591 // red stub
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

internal sealed class GitWorkspaceRebase : IWorkspaceRebase
{
    public ValueTask<Result<string>> Fetch(string worktree, string branch, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<bool>> Contains(string worktree, string ancestor, string descendant, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<WorkspaceRebased>> Rebase(string worktree, string onto, string? from, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Reset(string worktree, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();
}
