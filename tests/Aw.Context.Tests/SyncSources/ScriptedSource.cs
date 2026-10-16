using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Tests.SyncSources;

/// <summary>A source that forwards to another and lets a test hook each search: count them, delay them, advance a clock.</summary>
internal sealed class ScriptedSource(IContextSource inner) : IContextSource
{
    private int _searches;

    public int Searches => _searches;

    public Func<CancellationToken, Task>? OnSearch { get; set; }

    public SourceDescriptor Describe() => inner.Describe();

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _searches);
        if (OnSearch is { } hook)
        {
            await hook(cancellationToken).ConfigureAwait(false);
        }

        return await inner.Search(query, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => inner.Fetch(reference, cancellationToken);

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => inner.Versions(refs, cancellationToken);

    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
}
