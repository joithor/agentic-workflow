using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The finished line's reads against the list's filters (review run 2, r2-6): an answer for an earlier filter never fills the line under a newer one.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsFinishedReadTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TaskCompletionSource Hold(InstanceQuery query)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.InstancesGates[query] = gate;
        return gate;
    }

    [Fact]
    public Task A_finished_answer_for_the_old_filter_is_dropped_once_a_new_filter_is_asked() => UiThread.Run(async () =>
    {
        var list = new WorkflowsViewModel(_server, new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await list.Refresh(_api, Token);
        var recipe = (string)GoldenFiles.Node("workflow-instances", "recipe-filter")["instances"]![0]!["recipe"]!["id"]!;
        var oldFinished = Hold(new InstanceQuery(Root: true));
        var expanding = list.ToggleDone(Token);
        var newOpen = Hold(new InstanceQuery(Root: true, Open: true, Recipe: recipe));
        var narrowing = list.FilterRecipe(recipe, Token);

        oldFinished.SetResult();
        await expanding;

        Assert.Empty(list.DoneRows);
        newOpen.SetResult();
        await narrowing;
    }, Token);
}
