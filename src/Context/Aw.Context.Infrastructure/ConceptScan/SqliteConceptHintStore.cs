using Aw.Context.Core.ConceptScan;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.ConceptScan;

/// <summary>The concept hints in context.db (migration 0012_q110_concept_hint), one row per version.</summary>
internal sealed class SqliteConceptHintStore(ContextDatabase database) : IConceptHintStore
{
    public ValueTask<Result<Unit>> Save(int version, IReadOnlyList<ConceptHint> hints, DateTimeOffset scanned, CancellationToken cancellationToken) => throw new NotSupportedException(database.ToString());

    public ValueTask<Result<IReadOnlyList<ConceptHint>>> Read(int version, CancellationToken cancellationToken) => throw new NotSupportedException(database.ToString());
}
