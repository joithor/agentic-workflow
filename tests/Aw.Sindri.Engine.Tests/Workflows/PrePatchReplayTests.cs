using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A history recorded by main before the Step inputs (no 'step-inputs-*' marker, a Step with no inputs) replays on this build with no
/// non-determinism: the three patch markers are read once at the start and a history without them issues none of the new commands
/// (engine review decision: patch gate).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class PrePatchReplayTests
{
    [Fact]
    public async Task A_history_recorded_before_the_Step_inputs_replays_without_new_commands()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Workflows", "Fixtures", "prepatch-step-history.json"), TestContext.Current.CancellationToken);
        var history = WorkflowHistory.FromJson("T-1/step/1", json);
        var replayer = new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>());

        var result = await replayer.ReplayWorkflowAsync(history, throwOnReplayFailure: false, TestContext.Current.CancellationToken);

        Assert.Null(result.ReplayFailure);
        Assert.DoesNotContain("step-inputs", json, StringComparison.Ordinal);
        Assert.DoesNotContain("launch-failure-keeps-round", json, StringComparison.Ordinal);   // no marker: a failed launch in such a history counts its round as it did
        Assert.DoesNotContain("step-unavailable", json, StringComparison.Ordinal);   // no marker: a Step failure no transition takes parks needs-answer at once, as it was recorded
    }
}
