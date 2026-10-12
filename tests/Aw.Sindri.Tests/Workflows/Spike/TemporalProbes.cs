using System.Diagnostics;
using System.Globalization;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// Engine P0 step 0: what each pinned server supports of rev 8 §5, §10 and §11 (Updates with validators and id
/// deduplication, async activity completion, signals, child workflows, DelayAsync with time skipping). The same probes
/// run on both servers; the support table in docs/csharp/sindri-workflow.md ("Temporal spike (P0)") names these tests.
/// </summary>
/// <param name="server">The server under probe.</param>
public abstract class TemporalProbes(TemporalServerFixture server)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly WorkflowEnvironment _env = server.Environment;

    [Fact]
    public async Task Update_validator_rejects_and_a_repeated_update_id_applies_once()
    {
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

    [Fact]
    public async Task Async_completion_completes_the_activity_through_its_task_token()
    {
        var queue = Queue("async");
        var desk = new DeskTasks();
        await Run(queue, desk, async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((AsyncCompletionProbeWorkflow w) => w.RunAsync(), new WorkflowOptions($"async-{server.Kind}", queue));
            var taskToken = await desk.WaitFor("1.1", Token);

            await _env.Client.GetAsyncActivityHandle(taskToken).CompleteAsync("done");

            Assert.Equal("done", await handle.GetResultAsync());
            Assert.Equal(1, desk.Opened);
        });
    }

    [Fact]
    public async Task Signals_reach_the_workflow_and_wait_until_it_waits()
    {
        var queue = Queue("signal");
        await Run(queue, new DeskTasks(), async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((SignalProbeWorkflow w) => w.RunAsync(3), new WorkflowOptions($"signal-{server.Kind}", queue));

            await handle.SignalAsync(w => w.BumpAsync(1));
            await handle.SignalAsync(w => w.BumpAsync(2));

            Assert.Equal(3, await handle.GetResultAsync());
        });
    }

    [Fact]
    public async Task Child_workflow_returns_its_result_to_the_parent()
    {
        var queue = Queue("child");
        await Run(queue, new DeskTasks(), async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync((ParentProbeWorkflow w) => w.RunAsync(), new WorkflowOptions($"item-1/workflow.item/1-{server.Kind}", queue));

            Assert.Equal("parent:child", await handle.GetResultAsync());
        });
    }

    [Fact]
    public async Task DelayAsync_skips_time_only_on_the_time_skipping_server()
    {
        var queue = Queue("delay");
        await Run(queue, new DeskTasks(), async () =>
        {
            var delay = server.Kind == ServerKind.TimeSkipping ? TimeSpan.FromHours(4) : TimeSpan.FromSeconds(1);
            var handle = await _env.Client.StartWorkflowAsync((DelayProbeWorkflow w) => w.RunAsync(delay), new WorkflowOptions($"delay-{server.Kind}", queue));
            var wall = Stopwatch.StartNew();

            var slept = await handle.GetResultAsync();
            wall.Stop();

            Assert.True(slept >= delay, $"slept {slept} of workflow time, wanted {delay}");
            if (server.Kind == ServerKind.TimeSkipping)
            {
                Assert.True(_env.SupportsTimeSkipping);
                Assert.True(wall.Elapsed < TimeSpan.FromSeconds(1), $"a 4h timer took {wall.Elapsed} of wall time");
            }
            else
            {
                Assert.False(_env.SupportsTimeSkipping);
                Assert.True(wall.Elapsed >= delay - TimeSpan.FromMilliseconds(50), $"a {delay} timer took only {wall.Elapsed} of wall time");
            }

            TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"{server.Kind}: DelayAsync({delay}) took {wall.Elapsed.TotalMilliseconds:F0} ms wall"));
        });
    }

    private string Queue(string probe) => $"probe-{probe}-{server.Kind}";

    private async Task Run(string queue, DeskTasks desk, Func<Task> body)
    {
        var options = new TemporalWorkerOptions(queue)
            .AddWorkflow<UpdateProbeWorkflow>()
            .AddWorkflow<AsyncCompletionProbeWorkflow>()
            .AddWorkflow<SignalProbeWorkflow>()
            .AddWorkflow<ParentProbeWorkflow>()
            .AddWorkflow<ChildProbeWorkflow>()
            .AddWorkflow<DelayProbeWorkflow>()
            .AddAllActivities(new ProbeActivities(desk));
        using var worker = new TemporalWorker(_env.Client, options);
        await worker.ExecuteAsync(body, Token);
    }
}
