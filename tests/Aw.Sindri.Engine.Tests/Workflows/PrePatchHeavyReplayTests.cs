using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// An instance that began before the heavy allocator (no 'ln-wf3-heavy-slot' marker) and sits at a 'waitFor: { resource: heavy }' replays on this build with no
/// non-determinism: with the marker absent the wait is never granted, so it stays waiting until its timeout parks it, as it did.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class PrePatchHeavyReplayTests
{
    [Fact]
    public async Task A_history_waiting_on_heavy_from_before_the_allocator_replays_without_new_commands()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Workflows", "Fixtures", "prepatch-heavy-history.json"), TestContext.Current.CancellationToken);
        var history = WorkflowHistory.FromJson("T-1/t-heavy/1", json);
        var replayer = new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>());

        var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false, TestContext.Current.CancellationToken);

        Assert.Null(result.ReplayFailure);
        Assert.DoesNotContain("ln-wf3", json, StringComparison.Ordinal);
    }
}
