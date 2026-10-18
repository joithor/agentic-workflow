using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-sync-gaps' background service (scoping spec C5, owner decision 2026-10-18 "allow, show the gap, queue or start the sync"): when a configured intent source has never synced
/// it starts a sync job as <c>service:context-sync-gaps</c>, through the SyncSources command (gate, audit, authorization and 'command.handled'; never the runner). Reads stay pure: the lenses
/// and the history only show the gap. A running sync is never doubled (key 'sync': SND-JOB-409 is a quiet run). It looks again on its next tick after that job's 'job.finished' (it reads the
/// jobs view; it does not wait on the job), backs off after a failed sync and goes quiet once every source has been tried. A failure to start surfaces as a failed service run (needs-you 'failure:service:context-sync-gaps').
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="start">The SyncSources command chain.</param>
/// <param name="clock">Dates the backoff.</param>
internal sealed class ContextSyncGapsService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, IService<SyncContextSourcesRequest, JobStartedResponse> start, TimeProvider clock) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-sync-gaps");

    /// <summary>Runs without a profile entry, every minute: a tick is two view reads when nothing is missing.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)));

    /// <summary>The wait after the first failed sync; it doubles with each failure in a row, up to <see cref="LongestBackoff"/>.</summary>
    internal static TimeSpan FirstBackoff { get; } = TimeSpan.FromMinutes(5);

    internal static TimeSpan LongestBackoff { get; } = TimeSpan.FromHours(1);

    public ServiceName Identity() => ServiceId;

    /// <summary>
    /// The trigger seam (sindri-api condition (b) is open on whether a tick is acceptable): today the scheduler's tick calls <see cref="Evaluate"/>; an event trigger on
    /// 'job.finished' would call the same method and the built-in cadence would go.
    /// </summary>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return Evaluate(cancellationToken);
    }

    /// <summary>Looks for a never-synced source and starts one sync when nothing runs and no backoff holds; everything the service decides is here, whatever wakes it.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Worked when a sync was started, else quiet; or the start's refusal.</returns>
    internal async ValueTask<Result<ScheduledResult>> Evaluate(CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            return scope.IsError ? scope.Error : ScheduledResult.Quiet;
        }

        var sources = await engine.Sources(scope.Value, cancellationToken).ConfigureAwait(false);
        if (sources.IsError || !sources.Value.Sources.Any(Gap))
        {
            return sources.IsError ? sources.Error : ScheduledResult.Quiet;
        }

        var running = await jobs.List(JobState.Running, SyncGaps.SyncKind, cancellationToken).ConfigureAwait(false);
        if (running.IsError || running.Value.Count > 0)
        {
            return running.IsError ? running.Error : ScheduledResult.Quiet;
        }

        var recent = await jobs.List(state: null, SyncGaps.SyncKind, cancellationToken).ConfigureAwait(false);
        if (recent.IsError || BackingOff(recent.Value))
        {
            return recent.IsError ? recent.Error : ScheduledResult.Quiet;
        }

        var started = await start.Handle(new SyncContextSourcesRequest(), cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error.Code == SindriErrors.JobConflict.Code ? ScheduledResult.Quiet : started.Error;
        }

        return new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"ok: sync {started.Value.JobId.Value} started"));
    }

    /// <summary>A configured intent source that has never been tried: no sync, no failure recorded.</summary>
    private static bool Gap(Aw.Context.Core.Results.SourceStatusRow row) => row.Kind == SourceKind.Intent && row.Sync is { LastSync: null, Code: null };

    // The newest jobs first: count the failures in a row, wait FirstBackoff doubled for each more, from when the newest ended.
    private bool BackingOff(IReadOnlyList<JobRow> newestFirst)
    {
        var failures = newestFirst.TakeWhile(static j => j.State == JobState.Failed).ToList();
        if (failures.Count == 0 || failures[0].EndedAt is not { } ended)
        {
            return false;
        }

        var wait = TimeSpan.FromTicks(Math.Min(LongestBackoff.Ticks, FirstBackoff.Ticks << Math.Min(failures.Count - 1, 8)));
        return clock.GetUtcNow() < ended + wait;
    }
}
