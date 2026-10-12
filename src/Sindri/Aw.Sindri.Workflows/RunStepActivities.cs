using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's activities (engine spec rev 8 §5.1, rev 8.1 §5.3). 'LoadRecipe' (a local activity) reads the
/// approved recipe the workflow input pins, through <see cref="IApprovedRecipes"/>. 'RunStep' is the short activity an
/// 'active' state starts with the run token in its id: in the desk runtime it records the open desk task and returns,
/// and the workflow then waits for the 'complete' Update.
/// </summary>
/// <param name="approvals">The approved recipes.</param>
/// <param name="catalog">The build's block catalog.</param>
internal sealed class RunStepActivities(IApprovedRecipes approvals, BlockCatalog catalog)
{
    /// <summary>Reads the approved recipe and the descriptors of its blocks.</summary>
    /// <param name="recipe">The recipe the instance is pinned to.</param>
    /// <returns>The loaded recipe.</returns>
    [Activity("LoadRecipe")]
    public async Task<LoadedRecipe> LoadRecipeAsync(RecipeVersion recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var found = await approvals.Find(recipe.Id, recipe.Hash, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            throw new ApplicationFailureException($"{found.Error.Message}", errorType: found.Error.Code, nonRetryable: true);
        }

        var definition = found.Value.Recipe;
        var used = definition.States.Values.Select(static s => s.Step?.Use)
            .Concat(definition.Transitions.SelectMany(static t => t.Gates.Select(static g => (BlockId?)g.Use).Concat(t.Effects.Select(static e => (BlockId?)e.Use))))
            .OfType<BlockId>().ToHashSet();
        return new LoadedRecipe(found.Value.Version, definition, [.. catalog.Blocks.Where(b => used.Contains(b.Id))]);
    }

    /// <summary>
    /// Records the open desk task and returns (rev 8.1 §5.3). Engine P4 contract: it records nothing yet; unit (c) writes
    /// the task (and the first-desk-task notification is P6's).
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>Done.</returns>
    [Activity("RunStep")]
    public static Task RunStepAsync(StepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return Task.CompletedTask;
    }
}
