using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.RecordEval;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// 'recipe eval --record' (engine P7, owner answer Q6): a served parent with a child is written back as an eval case with the child's lines
/// ('child.parked', 'child.resumed', and the child's end as 'wait.satisfied'), and the recorded case passes under 'recipe eval'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RecordEvalTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 180_000)]
    public async Task A_served_parent_with_a_child_records_child_lines_and_the_recorded_case_passes()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _harness.Approve(ChildRecipes.Child());
        _ = _harness.Approve(ChildRecipes.Parent());
        await using var server = new SindriTimeSkippingServer();
        await server.InitializeAsync();
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var tree = new ChildTree(sindri, server.Environment, _harness);
            var parent = await tree.Start("workflow.t-parent", "T-95");
            var (_, address) = await ChildTree.OneChild(parent);
            var child = tree.At(address);
            _ = await WorkflowsHarness.Until(child, static v => v.State == "waiting");
            await tree.PastTheHour();
            _ = await WorkflowsHarness.Until(parent, static v => v.State == "parked");
            _ = await ChildTree.Resume(child);
            _ = await WorkflowsHarness.Until(parent, static v => v.State == "run" && v.Question is null);
            await ChildTree.Go(child);
            _ = await parent.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60), ct);

            var recorded = await sindri.GetRequiredService<IService<RecordEvalRequest, RecordEvalResponse>>().Handle(new RecordEvalRequest(parent.Id), ct);
            Assert.False(recorded.IsError, recorded.IsError ? recorded.Error.Message : null);
            var verdict = await Replay(sindri, recorded.Value.Text, ct);

            Assert.Contains("\"child.parked\"", recorded.Value.Text, StringComparison.Ordinal);
            Assert.Contains("\"child.resumed\"", recorded.Value.Text, StringComparison.Ordinal);
            Assert.Contains("\"wait.satisfied\"", recorded.Value.Text, StringComparison.Ordinal);
            Assert.True(verdict.Passed, verdict.Detail);
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task An_unknown_instance_is_refused_with_its_address_in_the_message()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sindri = _harness.Sindri();

        var recorded = await sindri.GetRequiredService<IService<RecordEvalRequest, RecordEvalResponse>>().Handle(new RecordEvalRequest("T-96/t-parent/1"), ct);

        Assert.True(recorded.IsError);
        Assert.Contains("T-96/t-parent/1", recorded.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>Runs the recorded case over the approved parent and its pinned child, as 'recipe eval' does.</summary>
    private static async Task<EvalVerdict> Replay(IServiceProvider sindri, string text, CancellationToken ct)
    {
        var approved = await sindri.GetRequiredService<IApprovedRecipes>().Latest(RecipeId.From("workflow.t-parent"), ct);
        var children = await sindri.GetRequiredService<RecipeRegistryReader>().Children(ct);
        var reading = EvalCaseReader.Read("recorded", text, sindri.GetRequiredService<JsonSerializerOptions>());
        return reading.Case is { } evalCase
            ? EvalRunner.Run(evalCase, approved.Value.Recipe, sindri.GetRequiredService<BlockCatalog>(), children.Value)
            : reading.Invalid!;   // why: a reading without a case has its invalid line
    }
}
