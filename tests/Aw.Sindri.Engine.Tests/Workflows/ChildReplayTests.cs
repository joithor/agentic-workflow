using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A history recorded before engine P7 replays with no child started: the 'p7-children' patch is absent from it, so a transition into a child wait is rejected as it was then. It needs no server.</summary>
[Lane(TestLane.Integration)]
public sealed class ChildReplayTests
{
    /// <summary>
    /// 'parent-child-pre-p7' was recorded with the P7 marker left out: 'workflow.t-gate' receives 't.go' and its transition into a child wait
    /// is rejected (no child recipes are pinned then), so the instance stays where it was. Replayed now, no child workflow may start.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task A_history_without_the_p7_marker_replays_without_starting_a_child()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/parent-child-pre-p7.json")!;
        using var reader = new StreamReader(stream);
        var history = Temporalio.Common.WorkflowHistory.FromJson("T-81/t-gate/1", await reader.ReadToEndAsync(ct));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }
}
