using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'RecordEvent' (engine spec rev 8.1 §5.6): mirrors one interpreter event into the ledger, fenced by the held lease and
/// idempotent on 'workflow:{workflow id}:{run id}:{activity id}'. Every Temporal activity, local or regular, has a
/// deterministic ActivityInfo.ActivityId, the same on every replay; no activity sees a history event id, so the id the ledger records (column 'temporal_event_id', UI E-30) is that activity id. The interpreter
/// runs it as a <b>local</b> activity, so it costs one marker per row: a replay reads the marker and never executes it
/// again, and the one case that runs it twice (a worker that dies after the append but before the workflow task
/// completes) has the same activity id, so S1's ledger refuses the repeated key and records nothing. Failures throw, so
/// Temporal retries without bound and the workflow waits for the ledger.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="events">The control plane's event factory (its scope, clock, ids and the host's JSON options).</param>
/// <param name="logger">Where a replay that mirrors a different event than the ledger holds is warned about.</param>
internal sealed partial class RecordEventActivities(ILedgerStore ledger, HeldLease lease, EventFactory events, ILogger<RecordEventActivities>? logger = null)
{
    /// <summary>
    /// How the interpreter runs it: a local activity with unbounded retries and backoff, so the workflow does not move on
    /// until the ledger has the event (engine spec rev 8 §5.6).
    /// </summary>
    public static LocalActivityOptions LocalOptions { get; } = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(10),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromMilliseconds(100),
            BackoffCoefficient = 2,
            MaximumInterval = TimeSpan.FromSeconds(30),
            MaximumAttempts = 0,
        },
    };

    /// <summary>The ledger idempotency key of the RecordEvent execution <paramref name="info"/> describes.</summary>
    /// <param name="info">The activity's info: its workflow id, run id and deterministic activity id.</param>
    /// <returns>'workflow:{workflow id}:{run id}:{activity id}'.</returns>
    public static string KeyOf(ActivityInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return $"workflow:{info.WorkflowId}:{info.WorkflowRunId}:{info.ActivityId}";
    }

    /// <summary>Appends <paramref name="record"/> under the key of this execution (<see cref="KeyOf"/>).</summary>
    /// <param name="record">The event.</param>
    /// <returns>Its ledger position, and whether it was already there.</returns>
    [Activity("RecordEvent")]
    public Task<LedgerAppend> RecordAsync(WorkflowEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var activity = ActivityExecutionContext.Current;
        return Record(record, KeyOf(activity.Info), activity.Info.ActivityId, activity.CancellationToken);
    }

    /// <summary>Appends <paramref name="record"/> under <paramref name="key"/>; the activity id is the row's 'temporal_event_id'.</summary>
    /// <param name="record">The event.</param>
    /// <param name="key">The ledger idempotency key.</param>
    /// <param name="activityId">The activity id.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Its ledger position, and whether it was already there.</returns>
    internal async Task<LedgerAppend> Record(WorkflowEventRecord record, string key, string activityId, CancellationToken cancellationToken)
    {
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the event waits for one");
        var payload = new WorkflowEventPayload(record.WorkflowId, record.From, record.To, record.Trigger, record.Outcome, record.Run, record.Detail);
        var own = events.Scope;
        var scope = new EventScope(record.Actor ?? own.Actor, record.Tenant ?? own.Tenant, record.Ring ?? own.Ring);
        var entry = events.Entry(record.Type, payload, key, record.Item, scope) with { TemporalEvent = activityId };   // the key's third part (UI E-30)
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (!appended.IsError && appended.Value.AlreadyRecorded && await Differs(appended.Value.Sequence, entry, cancellationToken).ConfigureAwait(false))
        {
            // a workflow-task retry replayed with other signals: the same activity id now mirrors another event, so it keeps its own row
            var other = entry with { IdempotencyKey = $"{key}:{ContentHash(entry)}" };
            appended = await ledger.Append(other, grant, cancellationToken).ConfigureAwait(false);
        }

        return appended.IsError
            ? throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}")
            : appended.Value;
    }

    /// <summary>Whether the row at <paramref name="sequence"/> is another event than <paramref name="entry"/> (type or payload).</summary>
    private async Task<bool> Differs(long sequence, LedgerEntry entry, CancellationToken cancellationToken)
    {
        var read = await ledger.ReadAfter(sequence - 1, 1, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            throw new InvalidOperationException($"{read.Error.Code}: {read.Error.Message}");   // not knowing is not 'same': the activity retries until the stored row can be compared
        }

        if (read.Value.Count == 0 || read.Value[0].Sequence != sequence)
        {
            throw new InvalidOperationException($"the ledger row {sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)} recorded under {entry.IdempotencyKey} can't be read back; the activity retries");
        }

        var stored = read.Value[0].Envelope;
        var differs = !string.Equals(stored.Type, entry.Envelope.Type, StringComparison.Ordinal) || !string.Equals(stored.Payload, entry.Envelope.Payload, StringComparison.Ordinal);
        if (differs)
        {
            ReplayDiffers(logger ?? NullLogger<RecordEventActivities>.Instance, entry.IdempotencyKey, stored.Type, entry.Envelope.Type);
        }

        return differs;
    }

    /// <summary>The first 12 hex digits of SHA-256 over exactly what <see cref="Differs"/> compares: the type and the serialized payload.</summary>
    private static string ContentHash(LedgerEntry entry) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(entry.Envelope.Type + "\n" + entry.Envelope.Payload)))[..12];

    [LoggerMessage(Level = LogLevel.Warning, Message = "RecordEvent replay under {Key} mirrors a different event than the ledger holds ({Stored} there, {Replayed} now); recording it under a content-hash key")]
    private static partial void ReplayDiffers(ILogger logger, string key, string stored, string replayed);
}
