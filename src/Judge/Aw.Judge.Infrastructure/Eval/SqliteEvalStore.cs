using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Eval;

/// <summary>IEvalStore over the 'eval_items' and 'labels' tables (migration 0004) of decisions.sqlite, and the decision tables its reads start from. Cutover unit 6.3 builds it.</summary>
internal sealed class SqliteEvalStore : IEvalStore
{
    /// <inheritdoc />
    public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken)
        => throw new NotSupportedException("SqliteEvalStore: cutover unit 6.3");
}
