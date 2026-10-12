using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IDocIndex"/>: a refresh counts the '.md' files under the repo's 'docs/' folder (the
/// fixture's doc globs) and stamps the time; the first refresh adds them, later ones keep them. It passes
/// <see cref="DocIndexContract{TIndex}"/>.
/// </summary>
/// <param name="clock">The clock it stamps refreshes with.</param>
public sealed class FakeDocIndex(TimeProvider clock) : IDocIndex
{
    private readonly Dictionary<string, DocIndexStatus> _repos = new(StringComparer.Ordinal);

    /// <summary>When set, Refresh answers this code for every repo (a repo that can't be read).</summary>
    public string? RefreshCode { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<RepoRefresh>> Refresh(ContextRepo repo, IReadOnlyList<string> globs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var known = _repos.GetValueOrDefault(repo.Name);
        if (RefreshCode is { } code)
        {
            _repos[repo.Name] = new DocIndexStatus(known?.Head, known?.LastSync, code, known?.Documents ?? 0, known?.Chunks ?? 0);
            return ValueTask.FromResult<Result<RepoRefresh>>(new RepoRefresh(repo.Name, Head: null, 0, 0, 0, 0, code));
        }

        var docs = Path.Combine(repo.Path, "docs");
        var count = Directory.Exists(docs) ? Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories).Count() : 0;
        var head = SourceVersion.From("fake-head");
        _repos[repo.Name] = new DocIndexStatus(head, clock.GetUtcNow(), Code: null, count, count);
        var added = known is null ? count : 0;
        return ValueTask.FromResult<Result<RepoRefresh>>(new RepoRefresh(repo.Name, head, added, Removed: 0, Kept: count - added, Blocked: 0, Code: null));
    }

    /// <inheritdoc />
    public ValueTask<Result<DocIndexStatus>> Status(ContextRepo repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return ValueTask.FromResult<Result<DocIndexStatus>>(_repos.GetValueOrDefault(repo.Name) ?? new DocIndexStatus(Head: null, LastSync: null, Code: null, 0, 0));
    }
}
