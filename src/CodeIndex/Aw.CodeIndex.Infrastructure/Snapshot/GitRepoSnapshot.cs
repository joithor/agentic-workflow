using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>The repo's tree at a commit, read with git only (spec §5.1).</summary>
internal sealed class GitRepoSnapshot : IRepoSnapshot
{
    /// <inheritdoc />
    public ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken)
    {
        _ = CleanGit.Prepare(repo.Path, redirectInput: false, "ls-tree");
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
