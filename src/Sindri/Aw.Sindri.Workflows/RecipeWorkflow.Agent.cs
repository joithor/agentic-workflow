using Aw.Sindri.Core.Workflow.Engine.Instances;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The agent runtime of <see cref="RecipeWorkflow"/> (engine P8 U3). Red stub: the interpreter arm exists and fails the instance until the runtime is written.</summary>
internal sealed partial class RecipeWorkflow
{
    private static Task<WorkflowInstance> RunAgentStep(WorkflowInstance instance, OpenRun open, Interpreting context) =>
        throw new ApplicationFailureException($"the agent runtime is not built (run {open.Token.Value})", nonRetryable: true);
}
