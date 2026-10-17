using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Testing.Jobs;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every job runtime honours (API P6 plan 1.9): the work runs outside the command gate, its progress lines are appended in order as
/// 'service:jobs', a cancel trips the work token, the work runs under the starting caller, a second start of a running kind and key is SND-JOB-409
/// naming the running job, and the runtime takes no heavy-job slot.
/// </summary>
public abstract class JobRuntimeContract
{
    /// <summary>A fresh runtime.</summary>
    /// <returns>The subject.</returns>
    protected abstract ValueTask<IJobRuntimeSubject> Create();

    private static readonly TimeSpan _hang = TimeSpan.FromSeconds(10);

    private static CallerContext Human() => A.Caller().Human().Build();

    private static Task<T> Bounded<T>(Task<T> task) => task.WaitAsync(_hang, TimeProvider.System, TestContext.Current.CancellationToken);   // a hang guard on the real clock, never the wait itself: the runtime's clock is a fake one

    private static Task Bounded(Task task) => task.WaitAsync(_hang, TimeProvider.System, TestContext.Current.CancellationToken);

    /// <summary>
    /// A job whose work is still running leaves the command gate free: Start returns although the work has not ended (the work waits on the runtime's fake clock, which
    /// nobody moves), and another command passes. A start that held the gate for the work would not return, and the bound fails the test.
    /// </summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_started_job_runs_outside_the_command_gate()
    {
        await using var runtime = await Create();

        var started = await Bounded(runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), Human()));
        await Bounded(runtime.Probe.WaitStarted);

        Expect.True(started.IsSuccess);
        Expect.True(await runtime.CommandGateIsFree());
        _ = await runtime.Cancel(started.Value.JobId);
        await runtime.Quiesce();
    }

    /// <summary>The lines arrive as 'job.progress' rounds 1, 2, 3 in order, written by 'service:jobs', then one 'job.finished'.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Progress_lines_are_appended_in_order_as_service_jobs()
    {
        await using var runtime = await Create();

        var started = await runtime.Start(new EchoJobRequest(Lines: 3, DelayMs: 0), Human());
        await runtime.Quiesce();

        Expect.True(started.IsSuccess);
        var progress = runtime.Events.Where(e => string.Equals(e.Envelope.Type, EventTypes.JobProgress, StringComparison.Ordinal)).ToList();
        Expect.Equal(3, progress.Count);
        Expect.Sequence(["service:jobs", "service:jobs", "service:jobs"], progress.Select(e => e.Envelope.Actor));
        var id = started.Value.JobId.Value;
        Expect.Sequence([$"job.progress:{id}:1", $"job.progress:{id}:2", $"job.progress:{id}:3"], progress.Select(e => e.IdempotencyKey));
        Expect.Equal(1, runtime.Events.Count(e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)));
    }

    /// <summary>Cancelling a running job trips the token the work was given.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Cancel_trips_the_work_token()
    {
        await using var runtime = await Create();
        var started = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), Human());

        var cancelled = await runtime.Cancel(started.Value.JobId);
        await runtime.Quiesce();

        Expect.True(cancelled.IsSuccess);
        Expect.Equal(1, runtime.Probe.Cancelled);
        Expect.Equal(1, runtime.Events.Count(e => string.Equals(e.Envelope.Type, EventTypes.JobCancelled, StringComparison.Ordinal)));
        Expect.Equal(0, runtime.Events.Count(e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)));
    }

    /// <summary>The work sees the caller that started it (its own session or repo checks still see the human).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_work_runs_under_the_starting_caller()
    {
        await using var runtime = await Create();
        var caller = Human();

        _ = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), caller);
        await runtime.Quiesce();

        Expect.Sequence([caller.Actor], runtime.Probe.Actors);
    }

    /// <summary>While one echo job runs, a second start of the same kind and key is SND-JOB-409 and names the running job.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_start_of_a_running_kind_and_key_is_SND_JOB_409()
    {
        await using var runtime = await Create();
        var first = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), Human());

        var second = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        Expect.Equal(SindriErrors.JobConflict.Code, second.Error.Code);
        Expect.Equal(first.Value.JobId.Value, second.Error.Extensions!.First(static e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Value.GetString());
        _ = await runtime.Cancel(first.Value.JobId);
        await runtime.Quiesce();
    }

    /// <summary>
    /// The runtime holds no heavy-job slot while a job runs: a job that needs one (BuildMapService) takes it itself. Read once the work is running, and
    /// the job must not have failed for want of one.
    /// </summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_runtime_takes_no_heavy_slot()
    {
        await using var runtime = await Create();
        var started = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), Human());
        await Bounded(runtime.Probe.WaitStarted);   // the work runs: a runtime that took a slot first would not have got here, or would have failed the job

        Expect.Equal(0, runtime.HeavySlotsHeld);
        _ = await runtime.Cancel(started.Value.JobId);
        await runtime.Quiesce();
        Expect.Equal(0, runtime.HeavySlotsHeld);
        Expect.Equal(0, runtime.Events.Count(static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)));   // no failed end either
    }
}
