using System.Diagnostics;
using System.Globalization;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// Engine P4a probes: what each pinned server supports of rev 8 §5, §10 and §11 (Updates with validators and id
/// deduplication, async activity completion, signals, child workflows, DelayAsync with time skipping). The same probes
/// run on both servers; the support table in docs/csharp/sindri-workflow.md ("Temporal foundation") names these tests.
/// </summary>
/// <param name="server">The server under probe.</param>
public abstract class TemporalProbes(TemporalServerFixture server)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly WorkflowEnvironment _env = server.Environment;

    [Fact(Timeout = 30_000)]
    public async Task Update_validator_rejects_and_a_repeated_update_id_applies_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var queue = Queue("update");
        await Run(queue, new DeskTasks(), async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((UpdateProbeWorkflow w) => w.RunAsync(), new WorkflowOptions($"update-{server.Kind}", queue));

            var first = await handle.ExecuteUpdateAsync(w => w.AnswerAsync("yes"), new WorkflowUpdateOptions("nonce-1"));
            var again = await handle.ExecuteUpdateAsync(w => w.AnswerAsync("yes"), new WorkflowUpdateOptions("nonce-1"));
            var rejected = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(
                () => handle.ExecuteUpdateAsync(w => w.AnswerAsync("bad"), new WorkflowUpdateOptions("nonce-2")));
            var answers = await handle.QueryAsync(w => w.Answers);
            await handle.SignalAsync(w => w.FinishAsync());
            var result = await handle.GetResultAsync();
            var history = await handle.FetchHistoryAsync();

            Assert.Equal((1, 1, 1, 1), (first, again, answers, result));
            Assert.Contains("SND-WF-003", rejected.InnerException?.Message ?? string.Empty, StringComparison.Ordinal);
            var kinds = history.Events.Select(e => e.EventType).ToList();
            Assert.Equal(1, kinds.Count(k => k == EventType.WorkflowExecutionUpdateAccepted));
            Assert.Equal(1, kinds.Count(k => k == EventType.WorkflowExecutionUpdateCompleted));
        });
    }

    [Fact(Timeout = 30_000)]
    public async Task Async_completion_completes_the_activity_through_its_task_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var queue = Queue("async");
        var desk = new DeskTasks();
        await Run(queue, desk, async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((AsyncCompletionProbeWorkflow w) => w.RunAsync(), new WorkflowOptions($"async-{server.Kind}", queue));
            var taskToken = await desk.WaitFor("1.1", ct);

            await _env.Client.GetAsyncActivityHandle(taskToken).CompleteAsync("done");

            Assert.Equal("done", await handle.GetResultAsync());
            Assert.Equal(1, desk.Opened);
        });
    }

    [Fact(Timeout = 30_000)]
    public async Task Signals_reach_the_workflow_and_wait_until_it_waits()
    {
        var ct = TestContext.Current.CancellationToken;
        var queue = Queue("signal");
        await Run(queue, new DeskTasks(), async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((SignalProbeWorkflow w) => w.RunAsync(3), new WorkflowOptions($"signal-{server.Kind}", queue));

            await handle.SignalAsync(w => w.BumpAsync(1));
            await handle.SignalAsync(w => w.BumpAsync(2));

            Assert.Equal(3, await handle.GetResultAsync());
        });
    }

    [Fact(Timeout = 30_000)]
    public async Task Child_workflow_returns_its_result_to_the_parent()
    {
        var ct = TestContext.Current.CancellationToken;
        var queue = Queue("child");
        await Run(queue, new DeskTasks(), async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((ParentProbeWorkflow w) => w.RunAsync(), new WorkflowOptions($"item-1/workflow.item/1-{server.Kind}", queue));

            Assert.Equal("parent:child", await handle.GetResultAsync());
        });
    }

    [Fact(Timeout = 30_000)]
    public async Task DelayAsync_skips_time_only_on_the_time_skipping_server()
    {
        var ct = TestContext.Current.CancellationToken;
        var queue = Queue("delay");

        // why: a server of its own. On the shared time-skipping server an activity another probe left open (even in a
        // terminated instance) keeps time skipping off for the server's life (finding F1), so the 4 h timer would run in
        // wall time. The probe measures the timer, not that leak.
        await using var env = await TemporalServerFixture.Start(server.Kind);
        await Run(env, queue, new DeskTasks(), async () =>
        {
            var delay = server.Kind == ServerKind.TimeSkipping ? TimeSpan.FromHours(4) : TimeSpan.FromSeconds(1);
            var handle = await env.Client.StartWorkflowAsync((DelayProbeWorkflow w) => w.RunAsync(delay), new WorkflowOptions($"delay-{server.Kind}", queue));
            var wall = Stopwatch.StartNew();

            var slept = await handle.GetResultAsync();
            wall.Stop();

            Assert.True(slept >= delay, $"slept {slept} of workflow time, wanted {delay}");
            if (server.Kind == ServerKind.TimeSkipping)
            {
                Assert.True(env.SupportsTimeSkipping);
                Assert.True(wall.Elapsed < TimeSpan.FromSeconds(1), $"a 4h timer took {wall.Elapsed} of wall time");
            }
            else
            {
                Assert.False(env.SupportsTimeSkipping);
                Assert.True(wall.Elapsed >= delay - TimeSpan.FromMilliseconds(50), $"a {delay} timer took only {wall.Elapsed} of wall time");
            }

            SpikeLog.Write(string.Create(CultureInfo.InvariantCulture, $"{server.Kind}: DelayAsync({delay}) took {wall.Elapsed.TotalMilliseconds:F0} ms wall"));
        });
    }

    [Fact(Timeout = 30_000)]
    public async Task Worker_restart_mid_step_resumes_with_the_same_run_token()
    {
        var ct = TestContext.Current.CancellationToken;

        var (resumed, desk, handle, view) = await RestartMidStep("resume", stickyFirstWorker: false, TimeSpan.FromSeconds(20), ct);

        var history = await handle.FetchHistoryAsync();
        var scheduled = history.Events.Where(e => e.EventType == EventType.ActivityTaskScheduled)
            .Select(e => e.ActivityTaskScheduledEventAttributes.ActivityId).ToList();
        Assert.True(resumed);
        Assert.Equal(["1.1", "2.1"], scheduled);
        Assert.Equal(2, desk.Opened);
        Assert.Equal(("checks", "2.1"), (view?.State, view?.Run));
    }

    /// <summary>
    /// A worker that exits with the instance in its sticky cache: the dev server moves the next task to the new worker;
    /// the time-skipping server keeps it on the dead sticky queue (no fallback, measured 20 s with a 1 s sticky timeout),
    /// so restart tests there run the first worker with MaxCachedWorkflows = 0 or use StartLocalAsync.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Worker_restart_from_a_sticky_cache_resumes_only_on_the_dev_server()
    {
        var ct = TestContext.Current.CancellationToken;

        var (resumed, _, _, _) = await RestartMidStep("sticky", stickyFirstWorker: true, TimeSpan.FromSeconds(3), ct);

        Assert.Equal(server.Kind == ServerKind.Local, resumed);
    }

    /// <summary>Worker A starts an instance and exits mid-Step; worker B completes run 1.1 and waits for run 2.1.</summary>
    private async Task<(bool Resumed, DeskTasks Desk, WorkflowHandle<SpikeStepWorkflow, SpikeView> Handle, SpikeView? View)> RestartMidStep(
        string name, bool stickyFirstWorker, TimeSpan within, CancellationToken ct)
    {
        var queue = Queue(name);
        var desk = new DeskTasks();
        var ledger = await SpikeLedger.InMemory(ct);
        var spike = new SpikeDesk(_env.Client, desk);
        var id = $"RESUME-{name}/workflow.step/1-{server.Kind}";
        WorkflowHandle<SpikeStepWorkflow, SpikeView>? handle = null;
        await SpikeWorker.Run(_env.Client, SpikeWorker.Options(queue, desk, ledger, stickyFirstWorker ? 1000 : 0), async () =>
        {
            handle = await spike.Start(id, queue, new SpikeRun("RESUME-1", SpikeEvals.WorkflowStep));
            await spike.Opened(id, "1.1", ct);
        }, ct);

        var resumed = true;
        SpikeView? view = null;
        var wall = Stopwatch.StartNew();
        await SpikeWorker.Run(_env.Client, SpikeWorker.Options(queue, desk, ledger), async () =>
        {
            await spike.Complete(id, "1.1", new SpikeReport("done", new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:output"] = "o" }), ct);
            try
            {
                await spike.Opened(id, "2.1", ct, within);
                view = await handle!.QueryAsync(w => w.View);
            }
            catch (TimeoutException)
            {
                resumed = false;
            }
        }, ct);
        SpikeLog.Write(string.Create(CultureInfo.InvariantCulture,
            $"{server.Kind}: restart (sticky first worker: {stickyFirstWorker}) -> run 2.1 opened: {resumed} after {wall.Elapsed.TotalMilliseconds:F0} ms"));

        // why: leave no running instance on the shared server. (Terminating doesn't give the time-skipping server its
        // time skipping back: run 2.1's open activity keeps it off for good, so the DelayAsync probe has its own server.)
        await handle!.TerminateAsync("probe over");
        return (resumed, desk, handle, view);
    }

    private string Queue(string probe) => $"probe-{probe}-{server.Kind}";

    private Task Run(string queue, DeskTasks desk, Func<Task> body) => Run(_env, queue, desk, body);

    private static async Task Run(WorkflowEnvironment env, string queue, DeskTasks desk, Func<Task> body)
    {
        var options = new TemporalWorkerOptions(queue)
            .AddWorkflow<UpdateProbeWorkflow>()
            .AddWorkflow<AsyncCompletionProbeWorkflow>()
            .AddWorkflow<SignalProbeWorkflow>()
            .AddWorkflow<ParentProbeWorkflow>()
            .AddWorkflow<ChildProbeWorkflow>()
            .AddWorkflow<DelayProbeWorkflow>()
            .AddAllActivities(new ProbeActivities(desk));
        using var worker = new TemporalWorker(env.Client, options);
        await worker.ExecuteAsync(body, Token);
    }
}
