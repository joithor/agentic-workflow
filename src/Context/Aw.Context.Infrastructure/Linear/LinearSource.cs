using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Stub (S-6 red).</summary>
internal sealed class LinearSource(LinearSettings settings, LinearSourceParts parts) : IContextSource
{
    public LinearSettings Settings { get; } = settings;

    public LinearSourceParts Parts { get; } = parts;

    public SourceDescriptor Describe() => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
}
