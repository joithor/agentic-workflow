using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Testing;

/// <summary>Item repos that answer like <paramref name="inner"/> except for one lookup, which fails as an unreadable ledger does (SND-LEDGER-002).</summary>
/// <param name="inner">The repos that answer the other lookups.</param>
/// <param name="failAt">The lookup that fails, counted from 1 over <see cref="IItemRepos.RepoOf"/> and <see cref="IItemRepos.Exists"/> together.</param>
public sealed class FailingItemRepos(IItemRepos inner, int failAt) : IItemRepos
{
    private int _lookups;

    /// <summary>The lookups asked so far.</summary>
    public int Lookups => Volatile.Read(ref _lookups);

    /// <inheritdoc/>
    public ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken) =>
        Fails() ? ValueTask.FromResult<Result<RepoName?>>(SindriErrors.LedgerUnavailable.With("ledger.db: database is locked")) : inner.RepoOf(item, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Exists(ItemId item, CancellationToken cancellationToken) =>
        Fails() ? ValueTask.FromResult<Result<bool>>(SindriErrors.LedgerUnavailable.With("ledger.db: database is locked")) : inner.Exists(item, cancellationToken);

    private bool Fails() => Interlocked.Increment(ref _lookups) == failAt;
}
