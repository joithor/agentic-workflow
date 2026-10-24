using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// What an agent's approval needs (owner q93, 2026-10-23): every eval case the registered recipe lists is present and passes over the
/// pure engine, as 'recipe eval' runs it. The recipe was validated when it registered. A recipe that lists no evals needs nothing more.
/// </summary>
/// <param name="registry">Resolves the recipe's children from the registry.</param>
/// <param name="catalog">This build's blocks.</param>
/// <param name="json">The package's AwJson options.</param>
internal sealed class AgentEvalGate(RecipeRegistryReader registry, BlockCatalog catalog, JsonSerializerOptions json)
{
    /// <summary>Runs <paramref name="files"/> against <paramref name="registered"/>.</summary>
    /// <param name="registered">The recipe being approved.</param>
    /// <param name="files">The eval files the caller sent.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Nothing when every listed case passes; SND-RECIPE-008 naming the first that is missing or fails.</returns>
    public async ValueTask<Result<Unit>> Prove(RegisteredRecipe registered, IReadOnlyList<EvalFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(files);
        var parsed = registered.Read(json);
        if (parsed.IsError)
        {
            return parsed.Error;
        }

        if (parsed.Value.Evals.Count == 0)
        {
            return Unit.Value;
        }

        var children = await registry.Children(cancellationToken).ConfigureAwait(false);
        if (children.IsError)
        {
            return children.Error;
        }

        var recipe = parsed.Value with { Hash = registered.Hash };
        foreach (var eval in recipe.Evals)
        {
            var file = files.FirstOrDefault(f => string.Equals(f.Path, eval, StringComparison.Ordinal));
            if (file is null)
            {
                return WorkflowErrors.RecipeEvalsFailing.With($"{recipe.Id.Value} lists eval {eval}, which the approval didn't carry (fix: approve with the recipe file, sindri recipe approve <file>)");
            }

            var reading = EvalCaseReader.Read(Path.GetFileNameWithoutExtension(eval), file.Text, json);
            var verdict = reading.Case is { } evalCase ? EvalRunner.Run(evalCase, recipe, catalog, children.Value) : reading.Invalid!;   // why: a reading without a case has its invalid line
            if (!verdict.Passed)
            {
                return WorkflowErrors.RecipeEvalsFailing.With(string.Create(CultureInfo.InvariantCulture, $"{recipe.Id.Value} eval {eval} fails at line {verdict.Line}: {verdict.Detail} (fix: sindri recipe eval <file>)"));
            }
        }

        return Unit.Value;
    }
}
