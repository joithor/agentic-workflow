using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// The per-repo index files (spec §4.1). An implementation must: publish only by swapping a pointer, so a reader sees the
/// old index or the new one, never a half-written one; keep a published index immutable; leave the live index alone when a
/// build is cancelled, killed or disposed unpublished; read the pointer without following symlinks and treat anything
/// unexpected, or another schema version, as damaged; and never create a file on a read.
/// </summary>
public interface IIndexStore
{
    /// <summary>Reads the header of the live index, with no other work.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The header, or null when there is no index; SND-CODEIDX-010 when the pointer or file is damaged or of another schema.</returns>
    public ValueTask<Result<IndexHeader?>> ReadHeader(RepoName repo, CancellationToken cancellationToken);

    /// <summary>Opens the live index read-only.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reader (dispose it); SND-CODEIDX-013 when there is none, 010 when damaged.</returns>
    public ValueTask<Result<IIndexReader>> OpenRead(RepoName repo, CancellationToken cancellationToken);

    /// <summary>Starts a build file.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="incremental">True to copy the live index first; a copy that fails its integrity check starts from scratch instead (see <see cref="IIndexBuild.Incremental"/>).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The build (dispose it; disposing without <see cref="IIndexBuild.Publish"/> deletes the build file); SND-CODEIDX-001 for a bad repo name.</returns>
    public ValueTask<Result<IIndexBuild>> BeginBuild(RepoName repo, bool incremental, CancellationToken cancellationToken);

    /// <summary>Deletes superseded files and leftovers (matching exactly the index file pattern) that no pointer names, that are not the running build's file and that are older than 10 minutes.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Sweep(RepoName repo, CancellationToken cancellationToken);
}
