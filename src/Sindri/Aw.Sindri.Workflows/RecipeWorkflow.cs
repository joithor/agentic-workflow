using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The recipe interpreter: the one workflow type that runs every recipe (engine spec rev 8 §5.1). Workflow code is
/// deterministic: no I/O, clock or random except through 'Workflow.*'. Engine P0 spike: the throwaway interpreter for
/// 'workflow.step'.
/// </summary>
[Workflow("RecipeWorkflow")]
internal sealed class RecipeWorkflow
{
    private readonly InstanceSnapshot _snapshot = new("starting", Run: null, Outcome: null);

    /// <summary>Interprets the recipe from its initial state to a terminal one.</summary>
    /// <param name="run">The instance.</param>
    /// <returns>The terminal state.</returns>
    [WorkflowRun]
    public Task<RecipeResult> RunAsync(RecipeRun run) =>
        throw new NotSupportedException("engine P0 unit (b): interpret workflow.step");

    /// <summary>The instance as status shows it (the 'GetInstance' Query, spec §5.1).</summary>
    /// <returns>The state, the open run token and the outcome.</returns>
    [WorkflowQuery("GetInstance")]
    public InstanceSnapshot GetInstance() => _snapshot;
}
