using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>A ledger that records every event but the audit row, as a disk filling up between the two appends would.</summary>
/// <param name="inner">The ledger behind it.</param>
internal sealed class HandledFailingLedger(ILedgerStore inner) : ILedgerStore
{
    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) =>
        entry.Envelope.Type == EventTypes.CommandHandled
            ? ValueTask.FromResult<Result<LedgerAppend>>(SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full"))
            : inner.Append(entry, grant, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        inner.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
}
