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
/// and the history only show the gap. A running sync is never doubled (key 'sync': SND-JOB-409 is a quiet run). It re-checks on the scheduler tick by reading the jobs view; the scheduler has no event trigger (sindri-api accepted this), backs off after a failed sync and goes quiet once every source has been tried. A failure to start surfaces as a failed service run (needs-you 'failure:service:context-sync-gaps').
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

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            return scope.IsError ? scope.Error : ScheduledResult.Quiet;
        }

        var sources = await engine.Sources(scope.Value, cancellationToken).ConfigureAwait(false);
        var running = sources.IsError ? null : await jobs.List(JobState.Running, SyncGaps.SyncKind, cancellationToken).ConfigureAwait(false);
        var recent = running is null ? null : await jobs.List(state: null, SyncGaps.SyncKind, cancellationToken).ConfigureAwait(false);
        if (sources.IsError || running!.IsError || recent!.IsError)
        {
            return sources.IsError ? sources.Error : running!.IsError ? running.Error : recent!.Error;
        }

        var finished = recent.Value.Where(static j => j.State != JobState.Running && j.EndedAt is not null).Select(static j => new SyncAttempt(j.State == JobState.Failed, j.EndedAt!.Value)).ToList();
        var action = SyncGapDecision.Decide(sources.Value.Sources.Any(Gap), running.Value.Count > 0, finished, clock.GetUtcNow());
        if (action != SyncGapAction.Start)
        {
            return ScheduledResult.Quiet;
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
}
