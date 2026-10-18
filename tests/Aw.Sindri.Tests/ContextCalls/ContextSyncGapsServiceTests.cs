using System.Reflection;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The 'context-sync-gaps' service (scoping spec C5, owner decision 2026-10-18) and the 'sync' it leaves on a not-synced row, over the jobs view in memory.</summary>
public sealed class ContextSyncGapsServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly Starter _start;
    private int _next;

    public ContextSyncGapsServiceTests()
    {
        _start = new Starter(_h);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ContextSyncGapsService> Service(bool context, params SourceStatusRow[] rows)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = context ? JsonDocument.Parse("{}").RootElement.Clone() : null } };
        _ = await _h.Profiles.Save(loaded, Ct);
        var engine = DispatchProxy.Create<IContextEngine, SourcesProxy>();
        ((SourcesProxy)(object)engine).View = new SourcesView(rows, SessionsEnabled: false, _now);
        return new ContextSyncGapsService(new ContextScopes(_h.Profiles, _h.Callers), engine, _jobs, _start, _h.Clock);
    }

    private static SourceStatusRow Onyx(DateTimeOffset? lastSync = null, string? code = null) =>
        new(SourceId.From("onyx"), SourceKind.Intent, Repo: null, SourceState.Degraded, "d", code, lastSync, Records: 0) { Sync = new SourceSyncStatus(lastSync, SourceState.Degraded, 0, 0, 0, 0, code) };

    private void Job(JobState state, TimeSpan endedAgo)
    {
        _next++;
        _jobs.Put(new JobRow(JobId.New(_h.Ids), SyncGaps.SyncKind, JobKey.From("sync"), Item: null, Repo: null, Service: null, state,
            "human:cli", _now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null, state == JobState.Failed ? "SND-CONTEXT-030" : null, Result: null, CancelledBy: null, Sequence: _next));
    }

    private async Task<Result<ScheduledResult>> Run(ContextSyncGapsService service)
    {
        var grant = await _h.HoldLease();
        using var caller = _h.Callers.EnterService("context-sync-gaps");
        return await service.Run(new ScheduledRun(grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Never_synced_source_starts_one_sync_of_every_source()
    {
        var service = await Service(context: true, Onyx());

        var result = await Run(service);

        Assert.False(result.Value.Idle);
        Assert.Equal(null, Assert.Single(_start.Requests).Source);
        Assert.Equal("service:context-sync-gaps", _start.Actors.Single());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_to_do_without_a_context_section_a_synced_source_or_a_failed_attempt()
    {
        var none = await Service(context: false, Onyx());
        var synced = await Service(context: true, Onyx(lastSync: _now));
        var failedOnce = await Service(context: true, Onyx(code: "SND-CONTEXT-010"));

        Assert.True((await Run(none)).Value.Idle);
        Assert.True((await Run(synced)).Value.Idle);
        Assert.True((await Run(failedOnce)).Value.Idle);
        Assert.Empty(_start.Requests);   // a quiet tick only reads views: the service holds no ledger or logger, and starts nothing
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_sync_is_never_doubled()
    {
        Job(JobState.Running, TimeSpan.Zero);
        var service = await Service(context: true, Onyx());

        var result = await Run(service);

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_sync_backs_off_and_each_failure_in_a_row_doubles_the_wait()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(3));   // newest: ended 3 minutes ago, wait 5
        var service = await Service(context: true, Onyx());
        var waiting = await Run(service);
        _h.Clock.Advance(TimeSpan.FromMinutes(3));   // 6 minutes after it ended
        var elapsed = await Run(service);

        Assert.True(waiting.Value.Idle);
        Assert.False(elapsed.Value.Idle);
        Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_failures_in_a_row_wait_ten_minutes_and_a_success_resets_the_count()
    {
        Job(JobState.Failed, TimeSpan.FromMinutes(20));
        Job(JobState.Failed, TimeSpan.FromMinutes(7));   // newest: wait 10 from 7 minutes ago
        var service = await Service(context: true, Onyx());
        var waiting = await Run(service);
        Job(JobState.Succeeded, TimeSpan.FromMinutes(1));   // newest of all
        var reset = await Run(service);

        Assert.True(waiting.Value.Idle);
        Assert.False(reset.Value.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_job_conflict_is_a_quiet_run_and_any_other_refusal_is_a_failed_one()
    {
        _start.Answer = SindriErrors.JobConflict.With("a sync job is already running");
        var service = await Service(context: true, Onyx());
        var conflict = await Run(service);
        _start.Answer = SindriErrors.JobsRateLimited.With("too many jobs");
        var refused = await Run(service);

        Assert.True(conflict.Value.Idle);
        Assert.Equal(SindriErrors.JobsRateLimited.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_running_sync_is_named_on_a_not_synced_row_only()
    {
        Job(JobState.Running, TimeSpan.Zero);
        var gaps = new SyncGaps(_jobs);
        var notSynced = new SearchedSource(SourceId.From("onyx"), SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161", AsOf: null);
        var ok = new SearchedSource(SourceId.From("git-history"), SearchedState.Ok, Reason: null, Code: null, _now);

        var marked = await gaps.Mark(new LensSearched(["a"], "v1", 0, [], [notSynced, ok], new SearchedBands(new BandSearched(true), new BandSearched(true), new BandSearched(true))), Ct);

        Assert.Equal("running", marked.Sources[0].Sync!.State);
        Assert.StartsWith("JOB-", marked.Sources[0].Sync!.JobId.Value, StringComparison.Ordinal);
        Assert.Null(marked.Sources[1].Sync);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gap_with_no_job_running_carries_no_sync()
    {
        Job(JobState.Succeeded, TimeSpan.FromMinutes(1));
        var notSynced = new SearchedSource(SourceId.From("onyx"), SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161", AsOf: null);

        var marked = await new SyncGaps(_jobs).Mark(new LensSearched(["a"], "v1", 0, [], [notSynced], new SearchedBands(new BandSearched(true), new BandSearched(true), new BandSearched(true))), Ct);

        Assert.Null(marked.Sources[0].Sync);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_service_is_a_built_in_that_may_start_a_job()
    {
        Assert.Equal("context-sync-gaps", ContextSyncGapsService.BuiltIn.Name.Value);
        var row = Aw.Sindri.Tests.Commands.Authorization.Rows.Of(ActionClass.Job, "SyncSources");
        Assert.Null(Authorization.Decide(ActionClass.Job, CallerContext.Service("context-sync-gaps", _h.Ids), row).Refusal);
    }

    private sealed class Starter(SliceHarness harness) : IService<SyncContextSourcesRequest, JobStartedResponse>
    {
        internal List<SyncContextSourcesRequest> Requests { get; } = [];

        internal List<string> Actors { get; } = [];

        internal Error? Answer { get; set; }

        public ValueTask<Result<JobStartedResponse>> Handle(SyncContextSourcesRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Actors.Add(harness.Callers.Current?.Actor ?? string.Empty);
            return ValueTask.FromResult<Result<JobStartedResponse>>(Answer is { } error
                ? error
                : new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }

    /// <summary>An engine that answers only 'sources': the one call the service makes.</summary>
    public class SourcesProxy : DispatchProxy
    {
        public SourcesView? View { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IContextEngine.Sources) ? ValueTask.FromResult<Result<SourcesView>>(View!) : throw new NotSupportedException(targetMethod?.Name);
    }
}
