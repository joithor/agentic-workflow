using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's activities (engine spec rev 8 §5.1, §5.3). 'LoadRecipe' (a local activity) reads and normalizes the
/// recipe. 'RunStep' is the activity an 'active' state starts: in the desk runtime it records the desk task's task
/// token under its run token and returns pending (async completion); 'sindri workflow complete --run &lt;token&gt;'
/// completes it through that task token. Engine P4a: recipes come from the home's recipes directory.
/// </summary>
/// <param name="reader">The recipe reader.</param>
/// <param name="catalog">The build's block catalog.</param>
/// <param name="home">Where the recipes and task tokens are.</param>
/// <param name="tokens">The run-token-to-task-token map.</param>
internal sealed class RunStepActivities(IRecipeReader reader, BlockCatalog catalog, WorkflowsHome home, TaskTokens tokens)
{
    /// <summary>Reads 'workflow.&lt;name&gt;.yaml' and returns its long form and the descriptors of its blocks.</summary>
    /// <param name="recipeId">The recipe id.</param>
    /// <returns>The loaded recipe.</returns>
    [Activity("LoadRecipe")]
    public async Task<LoadedRecipe> LoadRecipeAsync(string recipeId)
    {
        var file = Path.Combine(home.RecipesDirectory, recipeId + ".yaml");
        var text = await File.ReadAllTextAsync(file, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);
        var reading = reader.Read(text, Path.GetFileName(file));
        if (reading.Document is not { } document)
        {
            var first = reading.Findings.Count > 0 ? reading.Findings[0].ToLine() : "it doesn't bind";
            throw new ApplicationFailureException($"{file} doesn't read: {first}", nonRetryable: true);
        }

        var recipe = document.Recipe with { Hash = document.Hash };
        var used = recipe.States.Values.Select(static s => s.Step?.Use)
            .Concat(recipe.Transitions.SelectMany(static t => t.Gates.Select(static g => (BlockId?)g.Use).Concat(t.Effects.Select(static e => (BlockId?)e.Use))))
            .OfType<BlockId>().ToHashSet();
        return new LoadedRecipe(recipe, [.. catalog.Blocks.Where(b => used.Contains(b.Id))]);
    }

    /// <summary>Starts the Step and leaves the activity open for its report.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The report the human or the session completed it with.</returns>
    [Activity("RunStep")]
    public Task<StepReport> RunStepAsync(StepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        tokens.Save(run.Address, run.Token, ActivityExecutionContext.Current.Info.TaskToken);
        throw new CompleteAsyncException();
    }
}
