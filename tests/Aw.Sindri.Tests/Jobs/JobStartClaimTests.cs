using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The (kind, key) claim of a start is released on every way the start can end without a runner (API P6 review C1, T3).</summary>
[Lane(TestLane.Unit)]
public sealed class JobStartClaimTests
{
    private static CallerContext Human() => A.Caller().Human().Build();

    [Fact]
    public async Task A_start_without_the_lease_releases_its_claim()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();
        var grant = runtime.H.Held.Current!;
        runtime.H.Held.Clear();

        var refused = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());
        runtime.H.Held.Hold(grant);
        var again = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        Assert.True(refused.IsError);
        Assert.True(again.IsSuccess);
        await runtime.Quiesce();
    }

    [Fact]
    public async Task A_start_whose_append_throws_releases_its_claim()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();
        runtime.H.Store.AppendFailure = new InvalidOperationException("the disk went away");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human()));
        runtime.H.Store.AppendFailure = null;
        var again = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        Assert.True(again.IsSuccess);
        await runtime.Quiesce();
    }

    [Fact]
    public async Task An_aborted_request_still_records_the_start_it_began_and_runs_the_job()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();
        using var aborted = new CancellationTokenSource();
        runtime.H.Store.BeforeAppend = entry => { if (entry.Envelope.Type == EventTypes.JobStarted) { aborted.Cancel(); } };   // the client drops the POST while the start is being recorded

        _ = await Record.ExceptionAsync(() => runtime.StartWith(Human(), aborted.Token));   // the audit after the start sees the dead token; the start itself is not torn
        await runtime.Quiesce();

        _ = Assert.Single(runtime.Events, e => e.Envelope.Type == EventTypes.JobStarted);
        _ = Assert.Single(runtime.Events, e => e.Envelope.Type == EventTypes.JobFinished);
        var again = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());   // the key is free once the job has ended
        Assert.True(again.IsSuccess);
        await runtime.Quiesce();
    }

    [Fact]
    public async Task A_precheck_that_refuses_starts_nothing_and_takes_no_claim()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();

        var refused = await runtime.Start(new ProbeJobRequest("refused"), Human());

        Expect.Equal(SindriErrors.ServiceNotFound.Code, refused.Error.Code);   // AddJob's precheck is the context lane's synchronous 404 or 409
        Assert.DoesNotContain(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobStarted, StringComparison.Ordinal));
        Expect.Equal(0, runtime.Get<JobRegistry>().Count());
        Expect.Equal(0, runtime.Runner.Running);
    }
}
