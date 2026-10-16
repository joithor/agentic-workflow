using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Jobs;

/// <summary>
/// Folds the four job events into the 'jobs' view (API P6): one row per job, keyed by its id, each event through the pure fold in <see cref="JobRules"/>.
/// A progress, finish or cancel for a job with no row is a fold that can't continue (SND-VIEW-001): the ledger holds events the view can't explain.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class JobsProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Jobs];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.JobStarted, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobProgress, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobFinished, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobCancelled, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.JobStarted => await Started(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.JobProgress => await Change<JobProgress>(recorded, rows, static p => p.Job, JobRules.Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.JobFinished => await Change<JobFinished>(recorded, rows, static f => f.Job, JobRules.Fold, cancellationToken).ConfigureAwait(false),
            _ => await Change<JobCancelled>(recorded, rows, static c => c.Job, JobRules.Fold, cancellationToken).ConfigureAwait(false),
        };
    }

    private async ValueTask<Result<Unit>> Started(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var started = events.Payload<JobStarted>(recorded);
        if (started.IsError)
        {
            return started.Error;
        }

        var key = started.Value.Job.Value;
        var row = await rows.Get<JobRow>(ViewNames.Jobs, key, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Jobs, key, JobRules.Fold(row.Value, started.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Change<TPayload>(LedgerEvent recorded, IViewRows rows, Func<TPayload, JobId> job,
        Func<JobRow, TPayload, LedgerEvent, JobRow> fold, CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var key = job(payload.Value).Value;
        var row = await rows.Require<JobRow>(ViewNames.Jobs, key, recorded.Envelope.Type, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Jobs, key, fold(row.Value, payload.Value, recorded), cancellationToken).ConfigureAwait(false);
    }
}
