using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.History;

/// <summary>
/// Reads the landed changes (first-parent commits) that touched a set of paths, from git and never the working tree (S-H decision 8). A repo git can't read is
/// SND-CONTEXT-048; an upper bound the clone doesn't hold is SND-CONTEXT-110; a path with a control character is SND-CONTEXT-006.
/// </summary>
public interface IFileHistory
{
    /// <summary>The first-parent commits from the query's bound (or HEAD) back that changed any of its paths inside its window, newest first.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="query">The bound, paths, window and cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The changes with each commit's searched files; or SND-CONTEXT-048, 110 or 006.</returns>
    public ValueTask<Result<FileHistory>> Changes(ContextRepo repo, FileHistoryQuery query, CancellationToken cancellationToken);
}
