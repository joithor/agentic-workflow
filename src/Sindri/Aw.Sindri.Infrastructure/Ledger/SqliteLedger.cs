using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>The ledger's event log in SQLite: each append checks the lease epoch and the idempotency key in the same write transaction.</summary>
internal sealed class SqliteLedger(LedgerDatabase database, ILedgerSignal signal) : ILedgerStore
{
    internal ILedgerSignal Signal { get; } = signal;

    private readonly SqliteLedgerReader _reads = new(database);

    public async ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var lease = await LeaseReads.Current(begun.Value, cancellationToken).ConfigureAwait(false);
            if (lease.IsError || !LeaseDecision.Holds(lease.Value, grant))
            {
                return lease.IsError ? lease.Error : SindriErrors.LeaseStale.With(string.Create(CultureInfo.InvariantCulture,
                    $"epoch {grant.Epoch} of {grant.Holder} is not current; nothing was recorded"));
            }

            return await InsertOnce(begun.Value, entry, grant, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<LedgerAppend>> InsertOnce(SqliteUnitOfWork session, LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var earlier = await SqliteQueries.List(session, new SqlStatement(LedgerSql.SelectByKey, c => c.Bind("$key", entry.IdempotencyKey)),
            (IRowMapper<long>)CountMapper.Instance, cancellationToken).ConfigureAwait(false);
        if (earlier.IsError || earlier.Value.Count > 0)
        {
            return earlier.IsError ? LedgerDatabase.Translate(earlier.Error) : new LedgerAppend(earlier.Value[0], AlreadyRecorded: true);
        }

        var inserted = await SqliteQueries.One(session, Insert(entry, grant), (IRowMapper<long>)CountMapper.Instance, cancellationToken)
            .ConfigureAwait(false);
        if (inserted.IsError)
        {
            return LedgerDatabase.Translate(inserted.Error);
        }

        var committed = await session.Commit(cancellationToken).ConfigureAwait(false);
        return committed.IsError ? LedgerDatabase.Translate(committed.Error) : new LedgerAppend(inserted.Value, AlreadyRecorded: false);
    }

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        _reads.ReadAfter(sequence, max, cancellationToken);

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => _reads.Latest(type, cancellationToken);

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => _reads.Head(cancellationToken);

    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => database.Schema(cancellationToken);

    private static SqlStatement Insert(LedgerEntry entry, LeaseGrant grant)
    {
        var e = entry.Envelope;
        return new SqlStatement(LedgerSql.InsertEvent, c => c
            .Bind("$id", e.Id).Bind("$type", e.Type).Bind("$schemaVersion", e.SchemaVersion).Bind("$occurredAt", e.OccurredAt)
            .Bind("$actor", e.Actor).Bind("$tenant", e.Tenant).Bind("$ring", e.Ring).Bind("$item", entry.Item)
            .Bind("$correlationId", e.CorrelationId).Bind("$causationId", e.CausationId?.ToString("D"))
            .Bind("$traceId", e.TraceId).Bind("$epoch", grant.Epoch).Bind("$key", entry.IdempotencyKey).Bind("$payload", e.Payload).Bind("$temporalEventId", entry.TemporalEvent),
            $"event {e.Type}");
    }
}
