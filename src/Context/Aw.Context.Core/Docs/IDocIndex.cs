using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Docs;

/// <summary>The repo-docs snapshot: scrubbed doc chunks at each repo's HEAD, behind the repo-docs source's search (spec §5.2).</summary>
public interface IDocIndex
{
    /// <summary>
    /// Brings <paramref name="repo"/>'s chunks up to date with its HEAD under <paramref name="globs"/>, cleaned with
    /// <paramref name="scrubbers"/>: a new or changed document, and one stored under another ruleset, is cleaned again.
    /// </summary>
    /// <param name="repo">The repo.</param>
    /// <param name="globs">The repo-relative doc globs.</param>
    /// <param name="scrubbers">The call's scrubbers; their ruleset is stored with every document.</param>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>What changed (a repo that can't be read is a result with its code, not an error).</returns>
    public ValueTask<Result<RepoRefresh>> Refresh(ContextRepo repo, IReadOnlyList<string> globs, Scrubbers scrubbers, CancellationToken cancellationToken);

    /// <summary>The repo's index state.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="ruleset">The current ruleset: documents stored under another one count as <see cref="DocIndexStatus.Outdated"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Its state.</returns>
    public ValueTask<Result<DocIndexStatus>> Status(ContextRepo repo, ScrubRuleset ruleset, CancellationToken cancellationToken);
}
