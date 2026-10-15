using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Reads a repo's files at a commit, never the working tree, so a build is the same on every machine (S-4). A missing repo or commit is SND-CONTEXT-048.</summary>
public interface IRepoReader
{
    /// <summary>Lists the tracked files at a commit.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths from the repo root, ordinal order.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepoName repo, string commit, CancellationToken cancellationToken);

    /// <summary>Reads one file at a commit.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The path from the repo root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text.</returns>
    public ValueTask<Result<string>> Read(ContextRepoName repo, string commit, string path, CancellationToken cancellationToken);
}
