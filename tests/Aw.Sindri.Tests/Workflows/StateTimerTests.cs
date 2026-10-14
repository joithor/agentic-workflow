using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The state timers on the time-skipping server (engine spec §3.4, §3.8, §5.5, W20, X58; engine P5 unit (a)): a wait
/// state's timeout parks, escalates, or retries once and then parks; the dev profile's 1m desk timeout parks
/// 'workflow.dev-timeout' and 'answer --resume' gives it the full minute again on the same run; a timer that ran out
/// while no worker ran fires once the worker is back, and parks once.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StateTimerTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_wait_state_timeout_with_then_park_parks_timeout()
    {
        var (parked, events) = await Expire("park", "T-41", static v => v.State == "parked");

        Assert.Equal((ParkReason.Timeout, "waiting ran past 10m"), (parked.Question?.Reason, parked.Question?.Question));
        Assert.Equal(["workflow.started", "workflow.transition"], events);
    }

    [Fact]
    public async Task A_wait_state_timeout_with_then_escalate_escalates()
    {
        var (escalated, events) = await Expire("escalate", "T-42", static v => v.State == "escalated");

        Assert.Equal("waiting ran past 10m", escalated.Question?.Question);
        Assert.Equal(["workflow.started", "workflow.transition"], events);
    }

    [Fact]
    public async Task A_retry_rearms_once_and_the_second_timeout_parks()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.timer-retry", Timed("retry"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.timer-retry", "T-43", ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.State == "waiting");

            await env.DelayAsync(TimeSpan.FromMinutes(11), ct);
            var retried = await desk.Until(static v => v.State == "waiting");
            var once = await Rows(desk, 2);
            await env.DelayAsync(TimeSpan.FromMinutes(10), ct);
            var parked = await desk.Until(static v => v.State == "parked");

            Assert.Null(retried.Question);
            Assert.Equal(["workflow.started", "workflow.transition"], once);
            Assert.Equal((ParkReason.Timeout, "waiting ran past 10m"), (parked.Question?.Reason, parked.Question?.Question));
            Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], await Rows(desk, 3));
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task Workflow_dev_timeout_parks_after_the_1m_desk_timeout_and_resume_gives_it_the_full_minute_on_the_same_run()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        var profile = Directory.CreateDirectory(Path.Combine(_harness.Home, "profile")).FullName;
        await File.WriteAllTextAsync(Path.Combine(profile, "profile.yaml"), WorkflowsHarness.Shipped("dev/profile.yaml"), ct);
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-2", ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.Task is not null);

            await env.DelayAsync(TimeSpan.FromSeconds(75), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('3', 32)), new string('0', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
            var resumed = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('3', 32)));
            await env.DelayAsync(TimeSpan.FromSeconds(50), ct);
            var waiting = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromSeconds(15), ct);
            var again = await desk.Until(static v => v.State == "parked");

            Assert.Equal((ParkReason.Timeout, "hold ran past 1m", "1.1"), (parked.Question?.Reason, parked.Question?.Question, parked.Run?.Value));
            Assert.Equal(("hold", "1.1", true), (resumed.Instance.State, resumed.Instance.Run?.Value, resumed.Instance.Task is not null));
            Assert.Equal(("hold", "1.1", null), (waiting.State, waiting.Run?.Value, waiting.Question));
            Assert.Equal((ParkReason.Timeout, "1.1"), (again.Question?.Reason, again.Run?.Value));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task Expired_timer_fires_on_resume()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.timer-park", Timed("park"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var options = sindri.GetRequiredService<TemporalWorkerOptions>();
        var first = (TemporalWorkerOptions)options.Clone();
        first.MaxCachedWorkflows = 0;   // finding F2: the time-skipping server never moves a task off a dead worker's sticky queue
        WorkflowHandle<Infrastructure.Workflows.RecipeWorkflow, Infrastructure.Workflows.RecipeResult>? handle = null;
        using (var before = new TemporalWorker(env.Client, first))
        {
            await before.ExecuteAsync(async () =>
            {
                handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.timer-park", "T-44", ct));
                _ = await WorkflowsHarness.Until(handle, static v => v.State == "waiting");
            }, ct);
        }

        await env.DelayAsync(TimeSpan.FromHours(3), ct);   // the worker is down well past the 10m timeout (X58)
        using var after = new TemporalWorker(env.Client, options);
        await after.ExecuteAsync(async () =>
        {
            var desk = new DeskInstance(sindri, handle!); // why: the first worker's run set it before it returned
            var parked = await desk.Until(static v => v.State == "parked");
            await env.DelayAsync(TimeSpan.FromHours(1), ct);

            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.Equal(["workflow.started", "workflow.transition"], await Rows(desk, 2));
            Assert.Equal("parked", (await desk.Until(static _ => true)).State);
            await desk.Handle.TerminateAsync("test over");
        }, ct);
    }

    /// <summary>Starts the 'then: <paramref name="then"/>' recipe on <paramref name="item"/>, skips past its 10m timeout and reads the result.</summary>
    private async Task<(InstanceView View, IReadOnlyList<string> Events)> Expire(string then, string item, Func<InstanceView, bool> expired)
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.timer-" + then, Timed(then));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        return await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.timer-" + then, item, ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.State == "waiting");

            await env.DelayAsync(TimeSpan.FromMinutes(11), ct);
            var view = await desk.Until(expired);
            var events = await Rows(desk, 2);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
            return (view, events);
        }, ct);
    }

    /// <summary>The 'workflow.*' ledger rows once there are <paramref name="count"/> (the mirror follows the workflow task).</summary>
    private static async Task<IReadOnlyList<string>> Rows(DeskInstance desk, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        var rows = await desk.Events();
        while (rows.Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            rows = await desk.Events();
        }

        return rows;
    }

    /// <summary>A recipe whose one wait state times out after 10m with 'then: <paramref name="then"/>'.</summary>
    private static string Timed(string then) => string.Join('\n',
    [
        "schema: 1",
        "id: workflow.timer-" + then,
        "version: 1",
        "initial: waiting",
        "states:",
        "  waiting: { kind: wait, waitFor: { event: item.routed }, timeout: { after: 10m, then: " + then + " },",
        "             next: \"Waiting for the router to route {item}.\" }",
        "  done:    { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: waiting, to: done, on: wait.satisfied }",
    ]);
}
