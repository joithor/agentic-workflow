using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>IDecisionLog over decisions.sqlite (the decisions, decision_details and failures tables).</summary>
internal sealed class SqliteDecisionLog : IDecisionLog
{
    public ValueTask<Result<Unit>> Record(DecisionRecord decision, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.Record");

    public ValueTask<Result<Unit>> RecordFailure(FailureRecord failure, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.RecordFailure");

    public ValueTask<Result<DecisionRecord>> Find(DecisionId id, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.Find");

    public ValueTask<Result<Unit>> MarkUndone(DecisionId id, DateTimeOffset at, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.MarkUndone");

    public ValueTask<Result<Unit>> PrefixReasonCode(DecisionId id, string prefix, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.PrefixReasonCode");

    public ValueTask<Result<int>> CountFailuresSince(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.CountFailuresSince");

    public ValueTask<Result<IReadOnlyList<DecisionOutcome>>> LastAttemptOutcomes(int count, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.LastAttemptOutcomes");

    public ValueTask<Result<int>> PruneDetailsBefore(DateTimeOffset before, CancellationToken cancellationToken) => throw new NotSupportedException("red: SqliteDecisionLog.PruneDetailsBefore");
}
