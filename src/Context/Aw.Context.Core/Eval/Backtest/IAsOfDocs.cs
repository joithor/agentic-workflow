using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Opens a repo's docs as they were at a commit, as a throwaway source for the backtest's 'docs' and 'all' arms (S-7A). The live repo-docs source is unchanged.</summary>
public interface IAsOfDocs
{
    /// <summary>Opens the docs at the commit.</summary>
    /// <param name="request">The repo, commit, globs and scrubbers.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A source that answers searches over that commit's docs and says it supports as-of, or SND-CONTEXT-048 when git can't read the commit.</returns>
    public ValueTask<Result<IContextSource>> Open(AsOfDocsRequest request, CancellationToken cancellationToken);
}
