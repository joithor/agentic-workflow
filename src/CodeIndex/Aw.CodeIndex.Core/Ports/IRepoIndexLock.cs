using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// A per-repo lock over the index files (not the box-wide heavy-job slots): whoever copies the live index to write a new one
/// holds it until the new one is published or discarded, so a build and an embed run for the same repo never interleave and
/// neither discards the other's work. Different repos never wait on each other.
/// </summary>
public interface IRepoIndexLock
{
    /// <summary>Takes the repo's lock, waiting while another holder has it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="maxWait">The longest wait; zero fails at once when it is held.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The held lock (disposing releases it, more than once is fine), or SND-CODEIDX-016 when it stayed held past <paramref name="maxWait"/>.</returns>
    public ValueTask<Result<IAsyncDisposable>> Take(RepoName repo, TimeSpan maxWait, CancellationToken cancellationToken);
}
