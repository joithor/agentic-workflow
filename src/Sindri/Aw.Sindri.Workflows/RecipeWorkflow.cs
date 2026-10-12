using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The recipe interpreter: the one workflow type that runs every recipe (engine spec rev 8 §5.1). Workflow code is
/// deterministic: no I/O, clock or random except through 'Workflow.*'. P0 contract stub; engine P0 unit (b) writes
/// the throwaway interpreter for 'workflow.step'.
/// </summary>
[Workflow("RecipeWorkflow")]
internal sealed class RecipeWorkflow
{
    /// <summary>Interprets the recipe from its initial state to a terminal one.</summary>
    /// <param name="run">The instance.</param>
    /// <returns>The terminal state.</returns>
    [WorkflowRun]
    public Task<RecipeResult> RunAsync(RecipeRun run) =>
        throw new NotSupportedException("engine P0 unit (b): interpret workflow.step");
}
