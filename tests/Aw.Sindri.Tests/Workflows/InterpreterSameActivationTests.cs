using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Triggers that reach the interpreter in the same workflow task (engine spec §5.2, §5.3; review run 1, P4 interpreter).
/// The worker is stopped while the Updates are admitted (and the desk deadline passes), so the next worker's first
/// activation carries them all: a report that lands with the desk deadline wins over it, and the Updates validated in
/// the activation of the one that ends the instance are all answered, so the execution completes.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InterpreterSameActivationTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_report_in_the_activation_of_the_desk_deadline_wins_and_leaves_nothing_stale()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var handle = await StartOnDesk(sindri, "T-53", ct);
        var desk = new DeskInstance(sindri, handle);
        var report = Report("1.1", "done");
        await env.DelayAsync(TimeSpan.FromDays(3), ct);   // the 2d desk deadline passes while no worker runs
        var update = handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));   // waits for a worker
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // the server has it queued (an Update can't be started 'admitted')

        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var done = await update;
            var checks = await desk.Until(static v => v.State == "checks" && v.Task is not null);
            var next = Report("2.1", "passed");
            var again = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(next), new WorkflowUpdateOptions(new string('b', 32)));

            Assert.Equal("-> checks", done.Happened);
            Assert.Equal("2.1", checks.Run?.Value);
            Assert.Equal("-> verify", again.Happened);   // a stale report would have refused this one (SND-WF-005)
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task Two_cancels_in_one_workflow_task_are_both_answered_and_the_workflow_completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var handle = await StartOnDesk(sindri, "T-54", ct);
        var first = Settled(handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel('a')), new WorkflowUpdateOptions(new string('a', 32))));   // waits for a worker
        var second = Settled(handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel('b')), new WorkflowUpdateOptions(new string('b', 32))));
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // the server has both queued (an Update can't be started 'admitted')

        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30), ct);
            var result = await handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(30), ct);

            Assert.Equal(1, outcomes.Count(static o => o is null));   // one cancelled the instance
            Assert.Equal("cancelled", result.State);
        }, ct);
    }

    /// <summary>Starts a 'workflow.step' instance, waits for its desk task, then stops the worker that ran it.</summary>
    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> StartOnDesk(IServiceProvider sindri, string item, CancellationToken ct)
    {
        var env = server.Environment;
        using var first = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        WorkflowHandle<RecipeWorkflow, RecipeResult>? started = null;
        await first.ExecuteAsync(async () =>
        {
            started = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, item, ct));
            _ = await WorkflowsHarness.Until(started, static v => v.Task is not null);
            await ActivityDone(started, ct);
        }, ct);
        return started ?? throw new InvalidOperationException("the instance did not start");
    }

    /// <summary>Waits until the 'RunStep' activity has completed: time doesn't skip while one is open (spec §11 F1), and the worker mustn't stop mid-activity.</summary>
    private static async Task ActivityDone(WorkflowHandle handle, CancellationToken ct)
    {
        while (!(await handle.FetchHistoryAsync()).Events.Any(static e => e.EventType == Temporalio.Api.Enums.V1.EventType.ActivityTaskCompleted))
        {
            await Task.Delay(100, ct);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);   // the workflow task that follows it
    }

    private static StepCompleted Report(string run, string outcome)
    {
        var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
        var outputs = outcome == "done" ? new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output } : [];
        return new StepCompleted("human:cli", RunToken.From(run), OutcomeName.From(outcome), outputs, Tokens: 0, StepRuntime.Desk, new string('f', 64));
    }

    private static CancelRequested Cancel(char nonce) =>
        new("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string(nonce, 32)), new string(nonce, 64)), Sequence: null, Parent: null);

    /// <summary>The Update's failure, or null when it returned.</summary>
    private static async Task<WorkflowUpdateFailedException?> Settled(Task<CommandResult> update)
    {
        try
        {
            _ = await update;
            return null;
        }
        catch (WorkflowUpdateFailedException failed)
        {
            return failed;
        }
    }
}
