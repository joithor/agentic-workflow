using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.CancelJob;

/// <summary>CancelJob (API P6 unit E): job.cancelled is appended once for a running job, then the work's token trips; a session is refused; an ended or unknown job is a 409 or 404.</summary>
[Lane(TestLane.Unit)]
public sealed class CancelJobTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CallerContext Human() => A.Caller().Human().Build();

    internal static RealJobRuntime Runtime(InMemoryJobViews views) => new(scrubber: null, rows: ServiceRows.All, configure: (services, h) =>
        _ = services.AddSingleton<IJobViews>(views).AddSingleton<IJobCancellation>(sp => sp.GetRequiredService<IJobRunner>())
            .AddScoped<IValidator<CancelJobRequest>, AcceptAllValidator<CancelJobRequest>>()
            .AddCommand<CancelJobRequest, CommandAccepted, CancelJobService>());

    internal static CancelJobService Service(RealJobRuntime runtime, InMemoryJobViews views) =>
        new(views, runtime.H.Store, runtime.H.Held, runtime.H.Events, runtime.H.Callers, runtime.Runner);

    [Fact]
    public async Task CancelJob_appends_job_cancelled_and_trips_the_token()
    {
        var views = new InMemoryJobViews();
        await using var runtime = Runtime(views);
        await runtime.Open();
        var started = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), Human());
        views.Put(A.Job().WithId(started.Value.JobId).Build());
        using var entered = runtime.H.Callers.Enter(Human());

        var cancelled = await Service(runtime, views).Handle(new CancelJobRequest(started.Value.JobId), Token);
        await runtime.Quiesce();

        Assert.True(cancelled.IsSuccess, cancelled.IsError ? cancelled.Error.ToString() : null);
        Expect.Equal(1, runtime.Probe.Cancelled);   // the work saw its token trip
        var row = Assert.Single(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal));
        Expect.Equal(($"job.cancelled:{started.Value.JobId.Value}", "human:http", cancelled.Value.Sequence), (row.IdempotencyKey, row.Envelope.Actor, row.Sequence));
        Assert.DoesNotContain(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_cancelled_event_carries_the_repo_of_the_job()
    {
        var views = new InMemoryJobViews();
        await using var runtime = Runtime(views);
        await runtime.Open();
        views.Put(A.Job().InRepo(Core.Ids.RepoName.From("demo")).Build());
        using var entered = runtime.H.Callers.Enter(Human());

        var cancelled = await Service(runtime, views).Handle(new CancelJobRequest(A.FirstJobId), Token);

        Assert.True(cancelled.IsSuccess);
        var row = Assert.Single(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal));
        Expect.Equal(Core.Ids.RepoName.From("demo"), runtime.H.Events.Payload<JobCancelled>(row).Value.Repo);
    }

    [Fact]
    public async Task Cancel_from_a_session_is_not_refused()
    {
        await using var runtime = Runtime(new InMemoryJobViews());
        await runtime.Open();

        var refused = await runtime.Run<CancelJobRequest, CommandAccepted>(new CancelJobRequest(A.FirstJobId), A.Caller().Session().Build());

        Assert.NotEqual(SindriErrors.AuthRefused.Code, refused.IsError ? refused.Error.Code : null, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Cancel_on_a_finished_job_is_SND_JOB_409()
    {
        var views = new InMemoryJobViews();
        await using var runtime = Runtime(views);
        await runtime.Open();
        views.Put(A.Job().Ended(JobState.Succeeded).Build());
        using var entered = runtime.H.Callers.Enter(Human());

        var refused = await Service(runtime, views).Handle(new CancelJobRequest(A.FirstJobId), Token);

        Expect.Equal(SindriErrors.JobConflict.Code, refused.Error.Code);
        Assert.Empty(runtime.Events);
    }

    [Fact]
    public async Task Cancel_unknown_is_SND_JOB_404()
    {
        var views = new InMemoryJobViews();
        await using var runtime = Runtime(views);
        await runtime.Open();
        using var entered = runtime.H.Callers.Enter(Human());

        var refused = await Service(runtime, views).Handle(new CancelJobRequest(A.FirstJobId), Token);

        Expect.Equal(SindriErrors.JobNotFound.Code, refused.Error.Code);
    }

    [Fact]
    public async Task A_running_row_with_no_live_runner_is_still_ended_by_the_cancel()
    {
        var views = new InMemoryJobViews();
        await using var runtime = Runtime(views);
        await runtime.Open();
        views.Put(A.Job().Build());   // a row left running by a process that died
        using var entered = runtime.H.Callers.Enter(Human());

        var cancelled = await Service(runtime, views).Handle(new CancelJobRequest(A.FirstJobId), Token);

        Assert.True(cancelled.IsSuccess);
        _ = Assert.Single(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Race_finish_and_cancel_one_terminal_state()
    {
        for (var n = 0; n < 30; n++)
        {
            var views = new InMemoryJobViews();
            await using var runtime = Runtime(views);
            await runtime.Open();
            var started = await runtime.Start(new ProbeJobRequest("stubborn"), Human());   // the work ignores its token and returns when the latch is released
            await runtime.Latch.Entered.Task.WaitAsync(Token);
            views.Put(A.Job().WithId(started.Value.JobId).Build());
            using var barrier = new Barrier(2);
            var cancel = Task.Run(async () =>
            {
                barrier.SignalAndWait(Token);
                using var entered = runtime.H.Callers.Enter(Human());
                return await Service(runtime, views).Handle(new CancelJobRequest(started.Value.JobId), Token);
            }, Token);
            var finish = Task.Run(() =>
            {
                barrier.SignalAndWait(Token);
                _ = runtime.Latch.Release.TrySetResult();
            }, Token);

            _ = await cancel;
            await finish;
            await runtime.Quiesce();

            await AssertOneTerminalState(runtime, started.Value.JobId);
        }
    }

    // At most one job.finished and one job.cancelled, the view takes the first terminal event in ledger order, and nothing is left registered.
    private static async Task AssertOneTerminalState(RealJobRuntime runtime, Core.Ids.JobId job)
    {
        var terminals = runtime.Events.Where(e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal) || string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal)).ToList();
        Assert.InRange(terminals.Count(static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)), 0, 1);
        Assert.InRange(terminals.Count(static e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal)), 0, 1);
        Assert.NotEmpty(terminals);   // the job did end
        var views = new InMemoryViewStore(SliceHarness.Json);
        _ = (await views.Open(ViewSchema.Version, Token)).Value;
        _ = (await views.Apply(runtime.Events, [new JobsProjection(runtime.H.Events)], runtime.H.Clock.GetUtcNow(), Token)).Value;
        var row = (await views.List<JobRow>(ViewNames.Jobs, Token)).Value.Single(r => r.Id == job);
        Expect.Equal(string.Equals(terminals[0].Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal) ? JobState.Succeeded : JobState.Cancelled, row.State);
        Expect.Equal(0, runtime.Get<JobRegistry>().Count());
    }
}
