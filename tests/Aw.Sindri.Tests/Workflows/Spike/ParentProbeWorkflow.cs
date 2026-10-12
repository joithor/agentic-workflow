using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Child workflows (engine spec rev 8 §5.8): a child with id '{parent}/child.1' and ParentClosePolicy.Terminate.</summary>
[Workflow("ParentProbe")]
internal sealed class ParentProbeWorkflow
{
    /// <summary>True once the run has begun.</summary>
    [WorkflowQuery]
    public bool Started { get; private set; }

    /// <summary>Runs the child and returns its result.</summary>
    /// <returns>The child's result.</returns>
    [WorkflowRun]
    public Task<string> RunAsync()
    {
        Started = true;
        return Wf.ExecuteChildWorkflowAsync((ChildProbeWorkflow c) => c.RunAsync("parent"), new ChildWorkflowOptions
        {
            Id = Wf.Info.WorkflowId + "/child.1",
            ParentClosePolicy = ParentClosePolicy.Terminate,
        });
    }
}
