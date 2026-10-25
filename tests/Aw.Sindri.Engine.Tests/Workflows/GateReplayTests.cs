using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A history recorded before engine P7 replays with no gate activity: the 'p7-children' patch is absent from it, so the interpreter keeps deciding with no gate answers, as recorded. It needs no server.</summary>
[Lane(TestLane.Integration)]
public sealed class GateReplayTests
{
    /// <summary>
    /// 'item-gate-pre-p7' was recorded on the commit before any P7 interpreter change: 'workflow.item' receives 'item.routed',
    /// and its 'gate.route.startable' (no runtime then) refuses as unavailable. Replayed now, the transition must not start an
    /// 'EvaluateGates' activity the history doesn't hold.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task A_history_without_the_p7_marker_runs_no_gate_activity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/item-gate-pre-p7.json")!;
        using var reader = new StreamReader(stream);
        var history = Temporalio.Common.WorkflowHistory.FromJson("T-61/item/1", await reader.ReadToEndAsync(ct));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }
}
