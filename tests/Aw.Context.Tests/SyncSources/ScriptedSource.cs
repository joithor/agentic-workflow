using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.SyncSources;

/// <summary>A source that forwards to another and lets a test hook each search: count them, delay them, advance a clock.</summary>
internal sealed class ScriptedSource(IContextSource inner) : IContextSource
{
    private int _searches;

    public int Searches => _searches;

    public Func<CancellationToken, Task>? OnSearch { get; set; }

    /// <summary>Runs after the inner search answered, so a clock it advances can't cancel that search.</summary>
    public Action? AfterSearch { get; set; }

    /// <summary>Runs after the inner fetch answered, so a clock it advances can't cancel that fetch.</summary>
    public Action? AfterFetch { get; set; }

    public SourceDescriptor Describe() => inner.Describe();

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _searches);
        if (OnSearch is { } hook)
        {
            await hook(cancellationToken).ConfigureAwait(false);
        }

        var hits = await inner.Search(query, cancellationToken).ConfigureAwait(false);
        AfterSearch?.Invoke();
        return hits;
    }

    public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        var fetched = await inner.Fetch(reference, cancellationToken).ConfigureAwait(false);
        AfterFetch?.Invoke();
        return fetched;
    }

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => inner.Versions(refs, cancellationToken);

    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
}
