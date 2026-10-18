using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>
/// Searches a repo's files at a commit through 'git grep' (never the working tree), leaving out the paths the repo denies. The commit is resolved first
/// (110 when the clone doesn't hold it); the terms and paths go in chunks of <see cref="CodeRules.MaxTermsPerCall"/>; any other git failure is SND-CONTEXT-048.
/// </summary>
/// <param name="git">The read-only git calls.</param>
internal sealed class GitCodeSearch(GitCli git) : ICodeSearch
{
    public async ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var commit = query.Resolved ? Results.Success(query.Commit) : await git.Commit(query.Repo.Path, query.Commit, cancellationToken).ConfigureAwait(false);
        if (commit.IsError)
        {
            return Failure(query.Repo, commit.Error);
        }

        if (query.TermsProblem() is { } problem)
        {
            return problem;
        }

        var within = query.Within?.Where(p => !query.Repo.Denies(p)).Distinct(StringComparer.Ordinal).ToList();
        if (query.Terms.Count == 0 || within is { Count: 0 })
        {
            return new CodeSearchHits([], Truncated: false);
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var terms in query.Terms.Chunk(CodeRules.MaxTermsPerCall))
        {
            foreach (var paths in Chunks(within))
            {
                var grep = await git.GrepFiles(query.Repo.Path, commit.Value, terms, paths, cancellationToken).ConfigureAwait(false);
                if (grep.IsError)
                {
                    return Unreadable(query.Repo);
                }

                found.UnionWith(grep.Value.Where(p => !query.Repo.Denies(p)));
            }
        }

        var ordered = found.Order(StringComparer.Ordinal).ToList();
        return new CodeSearchHits([.. ordered.Take(query.Max)], ordered.Count > query.Max);
    }

    public async ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var resolved = await git.Commit(repo.Path, commit, cancellationToken).ConfigureAwait(false);
        return resolved.IsError ? Failure(repo, resolved.Error) : resolved;
    }

    private static IEnumerable<IReadOnlyList<string>?> Chunks(List<string>? within) =>
        within is null ? [null] : within.Chunk(CodeRules.MaxTermsPerCall).Cast<IReadOnlyList<string>?>();

    // 110 is the caller's to fix and says which repo; any other git failure is the checkout (048). Neither prints what git said.
    private static Error Failure(ContextRepo repo, Error error) =>
        error.Code == ContextErrors.CommitUnknown.Code
            ? ContextErrors.CommitUnknown.With($"the clone of {repo.Name.Value} holds no such commit, or an abbreviation of it is ambiguous or spelled like a ref (fix: fetch that commit into the profile repo's checkout, or pass the full sha)")
            : Unreadable(repo);

    private static Error Unreadable(ContextRepo repo) =>
        ContextErrors.ExtractionUnreadable.With($"git can't search {repo.Name.Value} (fix: check the checkout and that the commit exists)");
}
