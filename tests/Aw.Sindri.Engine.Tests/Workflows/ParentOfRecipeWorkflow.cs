using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A stand-in for the parent a child instance has (engine P7 starts real children): runs one RecipeWorkflow as its child on
/// Sindri's task queue, so the instance's 'Root' attribute reads false. It sets none of Sindri's search attributes itself.
/// </summary>
[Workflow("ParentOfRecipe")]
internal sealed class ParentOfRecipeWorkflow
{
    /// <summary>The task queue the parent runs on, apart from Sindri's so the serve worker never gets its tasks.</summary>
    public const string TaskQueue = "p4b-parents";

    /// <summary>The child this parent runs; set when the run begins.</summary>
    [WorkflowQuery]
    public InstanceStart? Child { get; private set; }

    /// <summary>Runs <paramref name="child"/> as a child workflow until it ends.</summary>
    /// <param name="child">The child's workflow input.</param>
    /// <returns>Done.</returns>
    [WorkflowRun]
    public Task RunAsync(InstanceStart child)
    {
        ArgumentNullException.ThrowIfNull(child);
        Child = child;
        return Wf.ExecuteChildWorkflowAsync(
            "RecipeWorkflow",
            [child],
            new ChildWorkflowOptions { Id = child.WorkflowId().Value, TaskQueue = TemporalSettings.DefaultTaskQueue, ParentClosePolicy = ParentClosePolicy.Terminate });
    }
}
