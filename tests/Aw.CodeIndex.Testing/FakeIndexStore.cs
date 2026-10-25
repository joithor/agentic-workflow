using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// The index files in memory (spec §11). Stub level: a publish swaps the live index whole, a build works on a copy, a
/// disposed unpublished build leaves the live index alone, and <see cref="Damage"/> makes a repo's index read as damaged.
/// CI-3 adds IndexStoreContract, which this fake and the SQLite store both pass.
/// </summary>
public sealed class FakeIndexStore : IIndexStore
{
    private readonly Dictionary<RepoName, FakeIndexData> _live = [];
    private readonly HashSet<RepoName> _damaged = [];

    /// <summary>Makes the repo's index read as damaged (SND-CODEIDX-010) until a build publishes a new one.</summary>
    /// <param name="repo">The repo.</param>
    public void Damage(RepoName repo) => _ = _damaged.Add(repo);

    /// <inheritdoc />
    public ValueTask<Result<IndexHeader?>> ReadHeader(RepoName repo, CancellationToken cancellationToken)
        => ValueTask.FromResult(
            _damaged.Contains(repo) ? Fail<IndexHeader?>()
            : _live.TryGetValue(repo, out var data) ? (Result<IndexHeader?>)data.Header
            : (Result<IndexHeader?>)(IndexHeader?)null);

    /// <inheritdoc />
    public ValueTask<Result<IIndexReader>> OpenRead(RepoName repo, CancellationToken cancellationToken)
        => ValueTask.FromResult(
            _damaged.Contains(repo) ? Fail<IIndexReader>()
            : _live.TryGetValue(repo, out var data) ? (Result<IIndexReader>)new FakeIndexReader(data.Copy())
            : CodeIndexErrors.IndexNotBuilt.With("no index for this repo yet"));

    /// <inheritdoc />
    public ValueTask<Result<IIndexBuild>> BeginBuild(RepoName repo, bool incremental, CancellationToken cancellationToken)
    {
        var from = incremental && !_damaged.Contains(repo) && _live.TryGetValue(repo, out var live) ? live : null;
        var data = from?.Copy() ?? new FakeIndexData();
        return ValueTask.FromResult<Result<IIndexBuild>>(new FakeIndexBuild(data, from?.Header, from is not null, published =>
        {
            _live[repo] = published;
            _ = _damaged.Remove(repo);
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Sweep(RepoName repo, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<Unit>>(Unit.Value);

    private static Result<T> Fail<T>() => CodeIndexErrors.IndexDamaged.With("the index file is damaged");
}
