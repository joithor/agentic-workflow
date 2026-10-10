using Aw.Kernel;

namespace Aw.Packets.Core.Ports;

/// <summary>Lists a repository's files at a commit (git today).</summary>
public interface IRepoSnapshot
{
    /// <summary>Every file path at <paramref name="commitSha"/>, repo-relative with '/' separators.</summary>
    /// <param name="repoDirectory">A directory inside the repository.</param>
    /// <param name="commitSha">A full commit sha.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths; AW-PACKETS-005 (not found) when the commit isn't in the repository; AW-PACKETS-006 when
    /// the repository can't be read (no repository, no git, a timeout).</returns>
    public ValueTask<Result<IReadOnlySet<string>>> Files(string repoDirectory, string commitSha, CancellationToken cancellationToken);
}
