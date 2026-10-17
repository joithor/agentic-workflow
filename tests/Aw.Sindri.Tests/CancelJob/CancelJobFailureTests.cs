using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Jobs;

namespace Aw.Sindri.Tests.CancelJob;

/// <summary>A cancel that could not be recorded takes its mark back, so the job writes its own end (API P6 review 2, CC-N1): exactly one terminal event, and the row is never left running.</summary>
[Lane(TestLane.Unit)]
public sealed class CancelJobFailureTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);   // a hang is the failure these tests look for; the bound only turns it into one

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CallerContext Human() => A.Caller().Human().Build();

    private static bool IsCancel(Core.Ledger.LedgerEntry entry) => string.Equals(entry.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal);

    private static async Task<(RealJobRuntime Runtime, InMemoryJobViews Views, JobId Job)> StartStubborn()
    {
        var views = new InMemoryJobViews();
        var runtime = CancelJobTests.Runtime(views);
        await runtime.Open();
        var started = await runtime.Start(new ProbeJobRequest("stubborn"), Human());   // the work ignores its token and returns when the latch is released
        await runtime.Latch.Entered.Task.WaitAsync(_bound, TimeProvider.System, Token);
        views.Put(A.Job().WithId(started.Value.JobId).Build());
        return (runtime, views, started.Value.JobId);
    }

    private static void AssertOnlyTheFinish(RealJobRuntime runtime)
    {
        _ = Assert.Single(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal));
        Assert.DoesNotContain(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal));
        Expect.Equal(0, runtime.Runner.Running);
        Expect.Equal(0, runtime.Get<Features.Jobs.JobRegistry>().Count());
    }

    [Fact]
    public async Task A_cancel_whose_append_errors_leaves_the_job_to_write_its_own_end()
    {
        var (runtime, views, job) = await StartStubborn();
        await using var disposing = runtime;
        using var released = new LatchRelease(runtime);   // a red run must fail, not hang the disposal behind a work that waits for ever
        runtime.H.Store.FailAppend = entry => IsCancel(entry) ? SindriErrors.LedgerUnavailable.With("busy") : null;
        using var entered = runtime.H.Callers.Enter(Human());

        var refused = await CancelJobTests.Service(runtime, views).Handle(new CancelJobRequest(job), Token);
        _ = runtime.Latch.Release.TrySetResult();
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);

        Expect.Equal(SindriErrors.LedgerUnavailable.Code, refused.Error.Code);
        Assert.False(runtime.Runner.IsCancelling(job));
        AssertOnlyTheFinish(runtime);
    }

    [Fact]
    public async Task A_cancel_whose_append_throws_leaves_the_job_to_write_its_own_end()
    {
        var (runtime, views, job) = await StartStubborn();
        await using var disposing = runtime;
        using var released = new LatchRelease(runtime);   // a red run must fail, not hang the disposal behind a work that waits for ever
        runtime.H.Store.BeforeAppend = entry => { if (IsCancel(entry)) { throw new InvalidOperationException("the request went away"); } };
        using var entered = runtime.H.Callers.Enter(Human());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await CancelJobTests.Service(runtime, views).Handle(new CancelJobRequest(job), Token));
        _ = runtime.Latch.Release.TrySetResult();
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);

        Assert.False(runtime.Runner.IsCancelling(job));
        AssertOnlyTheFinish(runtime);
    }

    [Fact]
    public async Task A_cancel_that_errors_after_the_work_has_returned_still_gets_the_job_its_finish()
    {
        var (runtime, views, job) = await StartStubborn();
        await using var disposing = runtime;
        using var released = new LatchRelease(runtime);   // a red run must fail, not hang the disposal behind a work that waits for ever
        runtime.H.Store.FailAppend = entry =>
        {
            if (!IsCancel(entry))
            {
                return null;
            }

            _ = runtime.Latch.Release.TrySetResult();   // the work returns while the cancel is being recorded, then the record fails
            WorkReturned.Await(runtime.Latch.Returned.Task, () => runtime.Runner.Running == 0);   // the job is parked on the pending cancel, or (red) already gone
            return SindriErrors.LedgerUnavailable.With("busy");
        };
        using var entered = runtime.H.Callers.Enter(Human());

        _ = await CancelJobTests.Service(runtime, views).Handle(new CancelJobRequest(job), Token);
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);

        AssertOnlyTheFinish(runtime);
    }

    [Fact]
    public async Task A_cancel_that_throws_after_the_work_has_returned_still_gets_the_job_its_finish()
    {
        var (runtime, views, job) = await StartStubborn();
        await using var disposing = runtime;
        using var released = new LatchRelease(runtime);   // a red run must fail, not hang the disposal behind a work that waits for ever
        runtime.H.Store.BeforeAppend = entry =>
        {
            if (IsCancel(entry))
            {
                _ = runtime.Latch.Release.TrySetResult();
                WorkReturned.Await(runtime.Latch.Returned.Task, () => runtime.Runner.Running == 0);   // the job is parked on the pending cancel, or (red) already gone
                throw new InvalidOperationException("the request went away");
            }
        };
        using var entered = runtime.H.Callers.Enter(Human());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await CancelJobTests.Service(runtime, views).Handle(new CancelJobRequest(job), Token));
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);

        AssertOnlyTheFinish(runtime);
    }

    [Fact]
    public async Task A_work_that_throws_a_cancellation_of_its_own_while_its_cancel_fails_to_record_gets_a_failed_end()
    {
        var views = new InMemoryJobViews();
        await using var runtime = CancelJobTests.Runtime(views);
        await runtime.Open();
        var started = await runtime.Start(new ProbeJobRequest("own-cancel"), Human());
        await runtime.Latch.Entered.Task.WaitAsync(_bound, TimeProvider.System, Token);
        views.Put(A.Job().WithId(started.Value.JobId).Build());
        using var released = new LatchRelease(runtime);
        runtime.H.Store.FailAppend = entry =>
        {
            if (!IsCancel(entry))
            {
                return null;
            }

            _ = runtime.Latch.Release.TrySetResult();   // the work throws its own cancellation while the cancel is pending, then the cancel is withdrawn
            WorkReturned.Await(runtime.Latch.Returned.Task);
            return SindriErrors.LedgerUnavailable.With("busy");
        };
        using var entered = runtime.H.Callers.Enter(Human());

        _ = await CancelJobTests.Service(runtime, views).Handle(new CancelJobRequest(started.Value.JobId), Token);
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);

        var finished = Assert.Single(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal));   // no cancel, no token trip: the job is not left running
        Expect.Equal(JobOutcome.Failed, runtime.H.Events.Payload<JobFinished>(finished).Value.Outcome);
        Assert.DoesNotContain(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancel_mark_that_arrives_after_the_job_decided_to_finish_is_ignored()
    {
        var (runtime, views, job) = await StartStubborn();
        await using var disposing = runtime;
        using var released = new LatchRelease(runtime);
        using var finishing = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        runtime.H.Store.BeforeAppend = entry =>
        {
            if (string.Equals(entry.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal))
            {
                finishing.Set();
                _ = proceed.Wait(_bound, Token);   // the end has decided and is writing
            }
        };
        _ = runtime.Latch.Release.TrySetResult();
        Assert.True(finishing.Wait(_bound, Token));

        runtime.Runner.Cancelling(job);   // too late to turn the end into "recorded elsewhere": no terminal event would follow if its append failed

        Assert.False(runtime.Runner.IsCancelling(job));
        proceed.Set();
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);
        AssertOnlyTheFinish(runtime);
    }

    [Fact]
    public async Task A_cancel_whose_request_is_aborted_is_still_recorded_and_the_job_ends_cancelled()
    {
        var (runtime, views, job) = await StartStubborn();
        await using var disposing = runtime;
        using var released = new LatchRelease(runtime);   // a red run must fail, not hang the disposal behind a work that waits for ever
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        using var entered = runtime.H.Callers.Enter(Human());

        var cancelled = await CancelJobTests.Service(runtime, views).Handle(new CancelJobRequest(job), aborted.Token);   // the append is not the request's to abort once the mark is set
        _ = runtime.Latch.Release.TrySetResult();
        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, Token);

        Assert.True(cancelled.IsSuccess, cancelled.IsError ? cancelled.Error.ToString() : null);
        _ = Assert.Single(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal));
        Assert.DoesNotContain(runtime.Events, static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal));
    }

    private sealed class LatchRelease(RealJobRuntime runtime) : IDisposable
    {
        public void Dispose() => _ = runtime.Latch.Release.TrySetResult();
    }
}
