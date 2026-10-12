using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>
/// 'recipe eval' (spec §6.3, §9.2, W27): reads the recipe and its child recipes beside it, then either folds the
/// engine's decisions over each eval case the recipe lists (paths relative to the recipe, never outside its directory,
/// S-L2), or scaffolds a happy-path case. A recipe that doesn't read, or an eval file that can't be read, is a tool
/// error; a malformed or diverging line fails its case.
/// </summary>
internal sealed class EvalRecipeService(IRecipeReader reader, IRecipeFiles files, BlockCatalog catalog, JsonSerializerOptions options)
    : IService<EvalRecipeRequest, EvalRecipeResponse>
{
    public async ValueTask<Result<EvalRecipeResponse>> Handle(EvalRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = await files.Read(request.Path, cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return text.Error;
        }

        var reading = reader.Read(text.Value, Path.GetFileName(request.Path));
        if (reading.Document is not { } document)
        {
            var first = (reading.Findings.Count > 0 ? reading.Findings[0].ToLine() : null) ?? "it doesn't bind";
            return WorkflowErrors.RecipeInvalid.With($"{request.Path} doesn't read: {first} (fix: sindri recipe validate {request.Path})");
        }

        var recipe = document.Recipe with { Hash = document.Hash };
        var children = await SameDirectoryChildren.Load(request.Path, recipe, reader, files, cancellationToken).ConfigureAwait(false);
        if (request.Mode == EvalMode.Scaffold)
        {
            return new EvalRecipeResponse([], EvalLines.Write(EvalScaffold.Generate(recipe, catalog, children), options));
        }

        var cases = new List<EvalCaseResult>(recipe.Evals.Count);
        foreach (var eval in recipe.Evals)
        {
            var result = await Run(request.Path, eval, recipe, children, cancellationToken).ConfigureAwait(false);
            if (result.IsError)
            {
                return result.Error;
            }

            cases.Add(result.Value);
        }

        return new EvalRecipeResponse(cases);
    }

    private async ValueTask<Result<EvalCaseResult>> Run(string path, string eval, Recipe recipe, IChildRecipeResolver children, CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(eval) || eval.Split('/', '\\').Contains("..", StringComparer.Ordinal))
        {
            return WorkflowErrors.RecipeEvalsFailing.With($"eval {eval} leaves the recipe's directory (fix: list it as evals/{recipe.Id.Value}/<case>.jsonl)");
        }

        var text = await files.Read(SameDirectoryChildren.Beside(path, eval), cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return WorkflowErrors.RecipeEvalsFailing.With($"eval {eval} can't be read: {text.Error.Message}");
        }

        var name = Path.GetFileNameWithoutExtension(eval);
        var reading = EvalCaseReader.Read(name, text.Value, options);
        var verdict = reading.Case is { } evalCase ? EvalRunner.Run(evalCase, recipe, catalog, children) : reading.Invalid!; // why: a reading without a case has its invalid line
        return new EvalCaseResult(eval, verdict.Passed, verdict.Line, verdict.Detail);
    }
}
