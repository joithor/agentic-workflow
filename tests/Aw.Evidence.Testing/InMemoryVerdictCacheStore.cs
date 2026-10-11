using Aw.Evidence.Core;
using Aw.Evidence.Core.Visual;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An in-memory IVerdictCacheStore, keyed by manifest path.</summary>
public sealed class InMemoryVerdictCacheStore : IVerdictCacheStore
{
    private readonly Dictionary<string, VerdictCache> _saved = new(StringComparer.Ordinal);

    /// <summary>When true, every save fails with AW-EVIDENCE-011.</summary>
    public bool Unwritable { get; set; }

    /// <summary>What has been saved, by path.</summary>
    public IReadOnlyDictionary<string, VerdictCache> Saved => _saved;

    /// <inheritdoc/>
    public ValueTask<VerdictCache> LoadAsync(string manifestPath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_saved.TryGetValue(manifestPath, out var cache) ? cache : VerdictCache.Empty);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SaveAsync(string manifestPath, VerdictCache cache, CancellationToken cancellationToken)
    {
        if (Unwritable)
        {
            return ValueTask.FromResult<Result<Unit>>(EvidenceErrors.FileUnavailable.With($"{manifestPath} is unwritable"));
        }

        _saved[manifestPath] = cache;
        return ValueTask.FromResult(Results.Ok());
    }
}
