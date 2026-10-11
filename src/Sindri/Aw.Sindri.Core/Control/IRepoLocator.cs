using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Control;

/// <summary>
/// Which profile repo a directory belongs to (port; API spec §3.1): the CLI's X-Sindri-Repo. The main checkout is the
/// parent of the directory's git common dir, so a worktree resolves to its repository; its real path is matched against
/// the repos' real paths (with '~' expanded), and the longest containing one wins (<see cref="RepoScope.Longest"/>).
/// </summary>
public interface IRepoLocator
{
    /// <summary>The repo <paramref name="directory"/> belongs to.</summary>
    /// <param name="directory">The working directory.</param>
    /// <param name="repos">The approved profile's repo entries.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The repo's name, or null outside git or outside every repo entry.</returns>
    public ValueTask<RepoName?> Locate(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken);
}
