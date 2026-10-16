using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>Stub.</summary>
internal sealed class AsOfRepoDocs : IContextSource
{
    public static ValueTask<Result<AsOfRepoDocs>> Build(AsOfDocsRequest request, IRepoReader reader, CancellationToken cancellationToken) => throw new NotSupportedException();

    public SourceDescriptor Describe() => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
}
