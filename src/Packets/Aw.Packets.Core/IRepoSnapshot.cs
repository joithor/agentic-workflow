using Aw.Kernel;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core;

/// <summary>Lists a repository's files at a commit (git today).</summary>
public interface IRepoSnapshot
{
    /// <summary>Every file path at <paramref name="commitSha"/>, repo-relative with '/' separators.</summary>
    /// <param name="repoDirectory">A directory inside the repository.</param>
    /// <param name="commitSha">The commit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths; AW-PACKETS-005 (not found) when the commit isn't in the repository; AW-PACKETS-006 when
    /// the repository can't be read (no repository, no git, a timeout).</returns>
    public ValueTask<Result<IReadOnlySet<string>>> Files(string repoDirectory, CommitSha commitSha, CancellationToken cancellationToken);
}
