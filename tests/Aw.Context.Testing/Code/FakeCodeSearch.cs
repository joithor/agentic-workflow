using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Testing.Code;

/// <summary>An in-memory repo at one commit that answers a file search; <see cref="CodeSearchContract"/> holds it to the git search's behavior.</summary>
/// <param name="known">The repo it answers for.</param>
/// <param name="head">Its only commit.</param>
/// <param name="files">The files at that commit.</param>
public sealed class FakeCodeSearch(ContextRepo known, string head, IReadOnlyDictionary<string, string> files) : ICodeSearch
{
    /// <inheritdoc/>
    public ValueTask<Result<string>> Resolve(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(commit);
        return ValueTask.FromResult<Result<string>>(repo.Name == known.Name && commit.Length >= 7 && head.StartsWith(commit, StringComparison.Ordinal) ? head : ContextErrors.CommitUnknown.With("the fake holds no such commit"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<CodeSearchHits>> FilesNaming(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Repo.Name != known.Name || !head.StartsWith(query.Commit, StringComparison.Ordinal) || query.Commit.Length < 7)
        {
            return ValueTask.FromResult<Result<CodeSearchHits>>(ContextErrors.CommitUnknown.With("the fake holds no such commit"));
        }

        if (query.TermsProblem() is { } problem)
        {
            return ValueTask.FromResult<Result<CodeSearchHits>>(problem);
        }

        var within = query.Within?.ToHashSet(StringComparer.Ordinal);
        var found = files.Where(f => !query.Repo.Denies(f.Key) && (within is null || within.Contains(f.Key)) && query.Terms.Any(t => f.Value.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.Key).Order(StringComparer.Ordinal).ToList();
        return ValueTask.FromResult<Result<CodeSearchHits>>(new CodeSearchHits([.. found.Take(query.Max)], found.Count > query.Max));
    }
}
