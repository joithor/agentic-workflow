using System.Globalization;
using Aw.Kernel.Outbox;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// The SQLite <see cref="IOutbox"/> over the <see cref="SqliteOutboxSchema"/> table. It runs on the session it is
/// given, so commands enqueued through a <see cref="SqliteUnitOfWork"/> commit or roll back with the state change that
/// recorded them (the transactional outbox, spec §4b.6). Time comes from <paramref name="clock"/>; failures are retried
/// by <paramref name="policy"/>. Times are stored and read back in UTC, and commands with the same enqueue time are read
/// in the order they were enqueued. Checked by the shared OutboxContract (Aw.Kernel.Testing).
/// </summary>
/// <param name="session">The session (normally the use case's unit of work).</param>
/// <param name="clock">Where 'now' comes from.</param>
/// <param name="policy">When a failed command is tried again.</param>
public sealed class SqliteOutbox(ISqliteSession session, TimeProvider clock, OutboxRetryPolicy policy) : IOutbox
{
    private const string Columns =
        "id, kind, payload_json, schema_version, enqueued_at, actor, tenant, ring, correlation_id, causation_id, trace_id";

    private const string InsertSql =
        "INSERT INTO outbox_commands (" + Columns + ") VALUES ($id, $kind, $payload, $schema, $enqueued, $actor, $tenant, $ring, $correlation, $causation, $trace) "
        + "ON CONFLICT (id) DO NOTHING";

    private const string Pending = "dispatched_at IS NULL AND dead_lettered_at IS NULL";

    private const string ReadPendingSql =
        "SELECT " + Columns + " FROM outbox_commands WHERE " + Pending + " AND (next_attempt_at IS NULL OR next_attempt_at <= $now) "
        + "ORDER BY enqueued_at, rowid LIMIT $max";

    private const string MarkDispatchedSql = "UPDATE outbox_commands SET dispatched_at = $at WHERE id = $id AND " + Pending;

    private const string AttemptsSql = "SELECT attempts FROM outbox_commands WHERE id = $id AND " + Pending;

    private const string PendingExistsSql = "SELECT EXISTS (SELECT 1 FROM outbox_commands WHERE id = $id AND " + Pending + ")";

    private const string MarkFailedSql =
        "UPDATE outbox_commands SET attempts = $attempts, last_error_code = $code, last_error_message = $message, "
        + "next_attempt_at = $next, dead_lettered_at = $dead WHERE id = $id AND attempts = $previous AND " + Pending;

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Enqueue(OutboxCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var inserted = await SqliteQueries.Execute(session, new SqlStatement(InsertSql, c => BindCommand(c, command), $"outbox command {command.Id}"), cancellationToken).ConfigureAwait(false);
        return inserted.Bind(rows => rows == 1
            ? Results.Ok()
            : KernelErrors.Conflict.With($"outbox command {command.Id} is already recorded"));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<OutboxCommand>>> ReadPending(int max, CancellationToken cancellationToken)
    {
        if (max < 1)
        {
            return KernelErrors.Invalid.With("ReadPending needs max >= 1");
        }

        var now = clock.GetUtcNow();
        return await SqliteQueries.List(
            session,
            new SqlStatement(ReadPendingSql, c => c.Bind("$now", now).Bind("$max", max), "pending outbox commands"),
            OutboxCommandMapper.Instance,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> MarkDispatched(Guid id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken)
    {
        var updated = await SqliteQueries.Execute(
            session, new SqlStatement(MarkDispatchedSql, c => c.Bind("$at", dispatchedAt).Bind("$id", id), $"outbox command {id}"), cancellationToken).ConfigureAwait(false);
        return updated.Bind(rows => rows == 1 ? Results.Ok() : NoSuchPending(id));
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> MarkFailed(Guid id, Error error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        var attempts = await SqliteQueries.One(
            session, new SqlStatement(AttemptsSql, c => c.Bind("$id", id), $"outbox command {id}"), AttemptsMapper.Instance, cancellationToken).ConfigureAwait(false);
        if (attempts.IsError)
        {
            return attempts.Error.Code == DataErrors.NotFound.Code ? NoSuchPending(id) : attempts.Error;
        }

        var now = clock.GetUtcNow();
        var failed = attempts.Value + 1;
        var next = policy.NextAttempt(failed, now);
        var updated = await SqliteQueries.Execute(
            session,
            new SqlStatement(
                MarkFailedSql,
                c => c.Bind("$attempts", failed).Bind("$code", error.Code).Bind("$message", error.Message)
                    .Bind("$next", next is null ? null : SqliteBind.Timestamp(next.Value))
                    .Bind("$dead", next is null ? SqliteBind.Timestamp(now) : null)
                    .Bind("$id", id).Bind("$previous", attempts.Value),
                $"outbox command {id}"),
            cancellationToken).ConfigureAwait(false);
        if (updated.IsError || updated.Value == 1)
        {
            return updated.Map(_ => Unit.Value);
        }

        // Another session changed the command between the read and the update (only possible outside a write
        // transaction): no longer pending is the documented not-found; still pending is a conflict to retry.
        var again = await SqliteQueries.Exists(
            session, new SqlStatement(PendingExistsSql, c => c.Bind("$id", id), $"outbox command {id}"), cancellationToken).ConfigureAwait(false);
        return again.Bind(pending => pending
            ? Results.Failure<Unit>(KernelErrors.Conflict.With($"outbox command {id} changed while its failure was being recorded; try again"))
            : NoSuchPending(id));
    }

    private static Error NoSuchPending(Guid id) => KernelErrors.NotFound.With($"no pending outbox command {id}");

    private static void BindCommand(SqliteCommand c, OutboxCommand command) =>
        c.Bind("$id", command.Id).Bind("$kind", command.Kind).Bind("$payload", command.PayloadJson)
            .Bind("$schema", command.SchemaVersion).Bind("$enqueued", command.EnqueuedAt).Bind("$actor", command.Actor)
            .Bind("$tenant", command.Tenant).Bind("$ring", command.Ring).Bind("$correlation", command.CorrelationId)
            .Bind("$causation", command.CausationId?.ToString("D", CultureInfo.InvariantCulture))
            .Bind("$trace", command.TraceId);
}
