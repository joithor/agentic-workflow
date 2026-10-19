using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Extract;

namespace Aw.Context.Features.GetCode;

/// <summary>The two steps both lexical rules share: ask git which files name a term (chunked), then read a candidate's text through the repo reader (deny globs enforced there).</summary>
internal sealed class LexicalCandidates(ICodeSearch search, IRepoReader repos)
{
    private const int Chunk = CodeRules.MaxTermsPerCall;

    /// <summary>The files within the query's paths that name any term, in ordinal order, at most the query's cap.</summary>
    /// <returns>The files and whether more matched; null after a failed search (the pass has noted it).</returns>
    internal async ValueTask<(List<string> Paths, bool Cut)?> Find(LexicalRun run, CandidateQuery query, CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        var cut = false;
        foreach (var terms in query.Terms.Chunk(Chunk))
        {
            foreach (var within in query.Within.Chunk(Chunk))
            {
                var hits = await search.FilesNaming(new CodeSearchQuery(run.Repo, run.Commit, terms, within, query.Max) { Resolved = true }, cancellationToken).ConfigureAwait(false);
                if (hits.IsError)
                {
                    run.Pass.Fail(hits.Error.Code, hits.Error.Code == ContextErrors.CommitUnknown.Code, query.Source);
                    return null;
                }

                paths.AddRange(hits.Value.Paths.Where(p => !run.Repo.Denies(p)));
                cut |= hits.Value.Truncated;
            }
        }

        var distinct = paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return (distinct.Take(query.Max).ToList(), cut || distinct.Count > query.Max);
    }

    /// <summary>A file's text at the run's commit (at most 512,000 characters), or null when it can't be read.</summary>
    internal async ValueTask<string?> Text(LexicalRun run, string path, CancellationToken cancellationToken)
    {
        if (run.Texts.TryGetValue(path, out var known))
        {
            return known;
        }

        var read = await repos.Read(run.Repo, run.Commit, path, cancellationToken).ConfigureAwait(false);
        var text = read.IsError ? null : read.Value.Length > CodeRules.MaxFileChars ? read.Value[..CodeRules.MaxFileChars] : read.Value;
        run.Texts[path] = text;
        return text;
    }
}
