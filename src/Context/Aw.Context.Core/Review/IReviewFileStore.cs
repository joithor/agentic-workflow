using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>The review files 'map review --export' writes and '--apply' reads (S-4): under the exports folder when written, anywhere outside every git worktree when read.</summary>
public interface IReviewFileStore
{
    /// <summary>Writes <paramref name="file"/> as JSON, 0600, by temp file and rename.</summary>
    /// <param name="path">A file name or a path under the exports folder, or null for <paramref name="defaultName"/>.</param>
    /// <param name="defaultName">The name to use when <paramref name="path"/> is null.</param>
    /// <param name="file">The review file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The absolute path written, SND-CONTEXT-005 inside a worktree, or SND-CONTEXT-033 outside the exports folder.</returns>
    public ValueTask<Result<string>> Write(string? path, string defaultName, ReviewFile file, CancellationToken cancellationToken);

    /// <summary>Reads a review file (YAML or JSON).</summary>
    /// <param name="path">The file's absolute path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file, SND-CONTEXT-005 inside a worktree, or SND-CONTEXT-031 when it can't be read or parsed.</returns>
    public ValueTask<Result<ReviewFile>> Read(string path, CancellationToken cancellationToken);
}
