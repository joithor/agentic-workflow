using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// A repo's tree at a commit (spec §5.1). An implementation must: read only git objects (never the working tree), change
/// nothing in the repo (no checkout, fetch or gc, no file written, no index lock), never touch a network, clear the git
/// environment variables and config the way the spec lists, keep only regular files, validate every path, and count what
/// it skips by <see cref="SkipReason"/> without keeping the skipped text.
/// </summary>
public interface IRepoSnapshot
{
    /// <summary>Lists the commit's kept files.</summary>
    /// <param name="repo">The repo and its checkout.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="limits">The deny globs and caps.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The inventory; SND-CODEIDX-002 when the path is not a git repo, git refuses it as unsafe or the commit is not in it; SND-CODEIDX-003 when the kept sizes pass the total cap, before any content is read.</returns>
    public ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken);

    /// <summary>Opens a reader over the repo's objects.</summary>
    /// <param name="repo">The repo and its checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reader (dispose it to end the process); SND-CODEIDX-002.</returns>
    public ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken);
}
