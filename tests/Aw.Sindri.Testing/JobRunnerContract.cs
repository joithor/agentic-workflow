using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing.Jobs;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every IJobRunner honours (API P6 review T8), real or fake: the work runs under the starting caller or, for a service's job, as
/// 'service:&lt;name&gt;'; the job context is set; the lines come in the order reported; a throw is a failed end with
/// <see cref="SindriErrors.ServiceFailed"/>; a cancel ends the work with no result and Start does not throw; Running counts running jobs.
/// </summary>
public abstract class JobRunnerContract
{
    private static readonly JobId _job = A.FirstJobId;
    private static readonly TimeSpan _hang = TimeSpan.FromSeconds(10);   // a runner that ignores a cancel would wait for ever on its fake clock: the bound turns that into a failure

    private static Task Bounded(Task task) => task.WaitAsync(_hang, TimeProvider.System, TestContext.Current.CancellationToken);

    /// <summary>A fresh runner.</summary>
    /// <returns>The subject.</returns>
    protected abstract ValueTask<IJobRunnerSubject> Create();

    private static CallerContext Human() => A.Caller().Human().Build();

    /// <summary>The work sees the caller that started the job.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_work_runs_under_the_starting_caller()
    {
        await using var subject = await Create();
        var caller = Human();

        await subject.Run(_job, new EchoJobRequest(Lines: 1, DelayMs: 0), caller, service: null);
        await subject.Settle();

        Expect.Sequence([caller.Actor], subject.Probe.Actors);
    }

    /// <summary>A service's job runs as 'service:&lt;name&gt;', not as the human who triggered it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_service_job_runs_as_the_service()
    {
        await using var subject = await Create();

        await subject.Run(_job, new EchoJobRequest(Lines: 1, DelayMs: 0), Human(), ServiceName.From("heartbeat"));
        await subject.Settle();

        Expect.Sequence(["service:heartbeat"], subject.Probe.Actors);
    }

    /// <summary>The job context the work reads is its own job's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_job_context_is_set_for_the_work()
    {
        await using var subject = await Create();

        await subject.Run(_job, new EchoJobRequest(Lines: 1, DelayMs: 0), Human(), service: null);
        await subject.Settle();

        Expect.Sequence([(JobId?)_job], subject.Probe.Jobs);
    }

    /// <summary>The lines are recorded in the order the work reported them, and the work ends with a result.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Lines_come_in_the_order_reported_and_the_job_succeeds()
    {
        await using var subject = await Create();

        await subject.Run(_job, new EchoJobRequest(Lines: 3, DelayMs: 0), Human(), service: null);
        await subject.Settle();

        Expect.Sequence(["echo 1", "echo 2", "echo 3"], subject.Lines(_job));
        Expect.Equal(new JobEnd(Succeeded: true, Code: null), subject.End(_job));
    }

    /// <summary>A work that throws ends as a failed job with the boundary code; Start does not throw.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_throw_is_a_failed_end_with_the_service_failed_code()
    {
        await using var subject = await Create();

        await subject.Run(_job, new EchoJobRequest(Lines: 1, DelayMs: 0, Throws: true), Human(), service: null);
        await subject.Settle();

        Expect.Equal(new JobEnd(Succeeded: false, SindriErrors.ServiceFailed.Code), subject.End(_job));
    }

    /// <summary>A cancel of a running job trips its token: the work ends with no result, Start does not throw, and nothing is running after.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancel_ends_the_work_with_no_result()
    {
        await using var subject = await Create();
        var run = subject.Run(_job, new EchoJobRequest(Lines: 100, DelayMs: 100), Human(), service: null);
        await subject.Probe.WaitStarted.WaitAsync(TestContext.Current.CancellationToken);

        Expect.Equal(1, subject.Runner.Running);
        Expect.True(subject.Runner.Cancel(_job));
        await Bounded(run);
        await subject.Settle();

        Expect.Equal(1, subject.Probe.Cancelled);
        Expect.True(subject.End(_job) is null);
        Expect.Equal(0, subject.Runner.Running);
    }

    /// <summary>A cancel of a job that is not running says so.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancel_of_an_unknown_job_is_false()
    {
        await using var subject = await Create();

        Expect.True(!subject.Runner.Cancel(_job));
    }

    /// <summary>A job marked as being cancelled says so until it is withdrawn.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancel_mark_is_visible_until_withdrawn()
    {
        await using var subject = await Create();
        var run = subject.Run(_job, new EchoJobRequest(Lines: 100, DelayMs: 100), Human(), service: null);
        await subject.Probe.WaitStarted.WaitAsync(TestContext.Current.CancellationToken);

        subject.Runner.Cancelling(_job);
        Expect.True(subject.Runner.IsCancelling(_job));
        subject.Runner.Withdraw(_job);
        Expect.True(!subject.Runner.IsCancelling(_job));

        _ = subject.Runner.Cancel(_job);
        await Bounded(run);
        await subject.Settle();
    }
}
