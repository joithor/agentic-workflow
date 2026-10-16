using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The interpreter's contract (engine spec rev 8.1 "Phase 4", §5.1, §5.3, §5.6) on the pinned dev server: the workflow
/// input is <see cref="InstanceStart"/>, the workflow id is the address, an active state's desk Step records its task
/// through a short 'RunStep' and waits for the 'complete' Update, and 'GetInstance' shows the open desk task with the
/// Step's declared outcomes and outputs and 'allowedCommands' as '{ actionId, cli }'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RecipeWorkflowTests : IDisposable
{
    private const string Address = "T-1/step/1";
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Start_opens_the_produce_desk_task_1_1_with_its_declared_outcomes_and_replays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.StepStart(sindri, "T-1", ct);
            var handle = await WorkflowsHarness.Start(env.Client, start);
            var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

            Assert.Equal((Address, "produce", "desk task", "1.1"), (handle.Id, view.State, view.Detail, view.Run?.Value));
            Assert.Equal(start.Recipe, view.Recipe);
            var task = view.Task!; // why: Until waited for it
            Assert.Equal("1.1", task.Run.Value);
            Assert.Contains("done", task.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
            Assert.Contains("fact:output", task.Outputs.Select(static o => o.Fact.Value), StringComparer.Ordinal);
            Assert.Equal("spec.md", task.Inputs.Single().Value.Name);
            Assert.Equal(["CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel"], [.. view.AllowedCommands.Select(static c => c.Action)]);
            Assert.Equal($"sindri workflow complete {Address} --run 1.1 --outcome done --output fact:output=<file>", view.AllowedCommands[0].Cli);

            var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>())
                .ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, ct);
            Assert.Null(replayed.ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    /// <summary>
    /// The worker 'sindri serve' runs (ITemporalWorker) runs the instance; the start is one ledger row keyed
    /// 'workflow:{workflow id}:{run id}:{activity id}' (spec rev 8.1 §5.6); 'workflow list' finds it by its search attributes.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task Serve_worker_mirrors_the_start_into_the_ledger_and_lists_the_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);   // 'workflow list' is a client of serve's route
        var sindri = served.Sindri;
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-1", ct));
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        Assert.Matches($"^{Address}  workflow\\.step@[0-9a-f]{{12}}  produce: .+  since .+\n$", await WorkflowsHarness.Listed(sindri, "NeedsYou = true AND Tenant = 'local'", ct));
        var runId = (await handle.DescribeAsync()).RunId;
        var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
            .Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();
        Assert.Equal(["workflow.started"], [.. rows.Select(static e => e.Envelope.Type)]);
        Assert.StartsWith($"workflow:{Address}:{runId}:", rows[0].IdempotencyKey, StringComparison.Ordinal);
        await handle.TerminateAsync("test over");
    }
}
