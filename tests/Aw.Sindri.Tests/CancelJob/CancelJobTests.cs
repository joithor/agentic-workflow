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

    private static RealJobRuntime Runtime(InMemoryJobViews views) => new(scrubber: null, rows: ServiceRows.All, configure: (services, h) =>
        _ = services.AddSingleton<IJobViews>(views).AddSingleton<IJobCancellation>(sp => sp.GetRequiredService<IJobRunner>())
            .AddScoped<IValidator<CancelJobRequest>, AcceptAllValidator<CancelJobRequest>>()
            .AddCommand<CancelJobRequest, CommandAccepted, CancelJobService>());

    private static CancelJobService Service(RealJobRuntime runtime, InMemoryJobViews views) =>
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
    public async Task Cancel_from_a_session_is_403()
    {
        await using var runtime = Runtime(new InMemoryJobViews());
        await runtime.Open();

        var refused = await runtime.Run<CancelJobRequest, CommandAccepted>(new CancelJobRequest(A.FirstJobId), A.Caller().Session().Build());

        Expect.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Contains("sindri jobs cancel", refused.Error.Message, StringComparison.Ordinal);
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
        var views = new InMemoryJobViews();
        await using var runtime = Runtime(views);
        await runtime.Open();
        var projection = new JobsProjection(runtime.H.Events);
        for (var n = 0; n < 30; n++)
        {
            var started = await runtime.Start(new EchoJobRequest(Lines: 0, DelayMs: 0), Human());
            views.Put(A.Job().WithId(started.Value.JobId).Build());
            using var barrier = new Barrier(2);
            var cancel = Task.Run(async () =>
            {
                barrier.SignalAndWait(Token);
                using var entered = runtime.H.Callers.Enter(Human());
                return await Service(runtime, views).Handle(new CancelJobRequest(started.Value.JobId), Token);
            }, Token);
            barrier.SignalAndWait(Token);
            _ = await cancel;
            await runtime.Quiesce();
        }

        var views2 = new InMemoryViewStore(SliceHarness.Json);
        _ = (await views2.Open(ViewSchema.Version, Token)).Value;
        _ = (await views2.Apply(runtime.Events, [projection], runtime.H.Clock.GetUtcNow(), Token)).Value;
        var rows = (await views2.List<JobRow>(ViewNames.Jobs, Token)).Value;
        Expect.Equal(30, rows.Count);
        Assert.All(rows, row => Assert.NotEqual(JobState.Running, row.State));   // every job ended in exactly one state, whoever won
    }
}
