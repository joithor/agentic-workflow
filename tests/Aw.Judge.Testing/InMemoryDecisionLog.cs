using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory IDecisionLog fake; it passes DecisionLogContract.</summary>
public sealed class InMemoryDecisionLog : IDecisionLog
{
    /// <inheritdoc />
    public ValueTask<Result<Unit>> Record(DecisionRecord decision, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.Record");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordFailure(FailureRecord failure, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.RecordFailure");

    /// <inheritdoc />
    public ValueTask<Result<DecisionRecord>> Find(DecisionId id, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.Find");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> MarkUndone(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.MarkUndone");

    /// <inheritdoc />
    public ValueTask<Result<Unit>> PrefixReasonCode(DecisionId id, string prefix, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.PrefixReasonCode");

    /// <inheritdoc />
    public ValueTask<Result<int>> CountFailuresSince(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.CountFailuresSince");

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<DecisionOutcome>>> LastAttemptOutcomes(int count, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.LastAttemptOutcomes");

    /// <inheritdoc />
    public ValueTask<Result<int>> PruneDetailsBefore(DateTimeOffset before, CancellationToken cancellationToken) => throw new NotSupportedException("red: InMemoryDecisionLog.PruneDetailsBefore");
}
