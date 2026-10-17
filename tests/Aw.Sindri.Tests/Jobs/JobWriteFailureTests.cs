using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>What the runner does when a write of its own fails (API P6 review C2, C3): the job always ends, and its claim and slot are always freed.</summary>
[Lane(TestLane.Unit)]
public sealed class JobWriteFailureTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);   // a hang is the failure these tests look for; the bound only turns it into one

    private static CallerContext Human() => A.Caller().Human().Build();

    private static JobFinished Finished(RealJobRuntime runtime) =>
        runtime.H.Events.Payload<JobFinished>(Assert.Single(runtime.Events, e => e.Envelope.Type == EventTypes.JobFinished)).Value;

    private static async Task<RealJobRuntime> Open()
    {
        var runtime = new RealJobRuntime();
        await runtime.Open();
        return runtime;
    }

    [Fact]
    public async Task A_writer_that_throws_still_frees_the_claim_and_ends_the_job_failed()
    {
        await using var runtime = await Open();
        runtime.H.Store.BeforeAppend = entry => { if (entry.Envelope.Type == EventTypes.JobProgress) { throw new InvalidOperationException("the writer broke"); } };
        _ = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal(0, runtime.Runner.Running);
        var finished = Finished(runtime);
        Expect.Equal((JobOutcome.Failed, SindriErrors.ServiceFailed.Code), (finished.Outcome, finished.Code));
        runtime.H.Store.BeforeAppend = null;
        Assert.True((await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human())).IsSuccess);   // the key is free
        await runtime.Quiesce();
    }

    [Fact]
    public async Task A_failed_end_write_is_tried_again_once()
    {
        await using var runtime = await Open();
        var refused = 0;
        runtime.H.Store.FailAppend = entry => entry.Envelope.Type == EventTypes.JobFinished && refused++ == 0 ? SindriErrors.LedgerUnavailable.With("busy") : null;
        _ = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal(JobOutcome.Succeeded, Finished(runtime).Outcome);
    }

    [Fact]
    public async Task An_end_write_that_keeps_failing_is_replaced_by_a_failed_end_with_the_write_code()
    {
        await using var runtime = await Open();
        var refused = 0;
        runtime.H.Store.FailAppend = entry => entry.Envelope.Type == EventTypes.JobFinished && refused++ < 2 ? SindriErrors.LedgerUnavailable.With("busy") : null;
        _ = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        var finished = Finished(runtime);
        Expect.Equal((JobOutcome.Failed, SindriErrors.LedgerUnavailable.Code), (finished.Outcome, finished.Code));
        Expect.Equal(0, runtime.Runner.Running);
    }

    [Fact]
    public async Task A_progress_write_that_fails_once_is_tried_again_and_the_job_goes_on()
    {
        await using var runtime = await Open();
        var refused = 0;
        runtime.H.Store.FailAppend = entry => entry.Envelope.Type == EventTypes.JobProgress && refused++ == 0 ? SindriErrors.LedgerUnavailable.With("busy") : null;
        _ = await runtime.Start(new EchoJobRequest(Lines: 2, DelayMs: 0), Human());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        Expect.Equal(2, runtime.Events.Count(e => e.Envelope.Type == EventTypes.JobProgress));
        Expect.Equal(JobOutcome.Succeeded, Finished(runtime).Outcome);
    }

    [Fact]
    public async Task A_progress_write_that_keeps_failing_ends_the_job_failed_with_the_write_code()
    {
        await using var runtime = await Open();
        runtime.H.Store.FailAppend = entry => entry.Envelope.Type == EventTypes.JobProgress ? SindriErrors.LedgerUnavailable.With("busy") : null;
        _ = await runtime.Start(new EchoJobRequest(Lines: 2, DelayMs: 0), Human());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        var finished = Finished(runtime);
        Expect.Equal((JobOutcome.Failed, SindriErrors.LedgerUnavailable.Code), (finished.Outcome, finished.Code));
    }

    [Fact]
    public async Task A_progress_write_that_fails_with_the_lease_gone_cancels_the_job_without_a_stop()
    {
        await using var runtime = await Open();
        runtime.H.Store.FailAppend = entry =>
        {
            if (entry.Envelope.Type != EventTypes.JobProgress)
            {
                return null;
            }

            runtime.H.Held.Clear();   // the lease went with the write: this is recovery's job, not a retry's
            return SindriErrors.LedgerUnavailable.With("stale lease");
        };
        _ = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), Human());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.False(runtime.Shutdown.Requested);   // nothing asked the process to stop: the failed write alone tripped the job's token
        Expect.Equal(1, runtime.Probe.Cancelled);
        Expect.Equal(0, runtime.Events.Count(static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)));
        Expect.Equal(0, runtime.Get<JobRegistry>().Count());   // the claim is free
        Expect.Equal(0, runtime.Runner.Running);
    }
}
