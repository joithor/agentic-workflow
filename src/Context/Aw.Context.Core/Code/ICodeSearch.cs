using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Code;

/// <summary>
/// File-level text search at a commit (S-CL): which files name any of some terms. It reads the commit through git, never the working tree, and
/// never returns a path the repo denies. A match is a path; no line or file text leaves it.
/// </summary>
public interface ICodeSearch
{
    /// <summary>The files at the query's commit whose text contains any term, without regard to case (substrings; the caller applies word boundaries).</summary>
    /// <param name="query">What to search for and where.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The paths; SND-CONTEXT-110 when the clone holds no such commit; SND-CONTEXT-006 for a term that is empty, too long or has a control character; SND-CONTEXT-048 when git cannot read the checkout.</returns>
    public ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken);

    /// <summary>Resolves a commit (full or abbreviated sha) to its full sha once, so a search that follows need not ask again.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The full sha; SND-CONTEXT-110 when the clone holds no such commit; SND-CONTEXT-048 when git cannot read the checkout.</returns>
    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken);
}
