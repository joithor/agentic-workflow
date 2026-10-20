using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>
/// 'recipe eval --shipped' (setup-once spec §5.3): runs the eval cases of every recipe in the embedded set over the pure engine, as 'recipe eval &lt;file&gt;' does for one file.
/// The publish runs it with the freshly published binary, and a failing case fails the publish. A recipe that does not read, whose eval file is not in the set, or that declares
/// no evals fails (approval needs evals declared, so a shipped recipe must have them). An empty embedded set passes.
/// </summary>
internal sealed class EvalShippedRecipesService(IEmbeddedRecipes embedded, IRecipeReader reader, BlockCatalog catalog, JsonSerializerOptions options)
    : IService<EvalShippedRequest, EvalShippedResponse>
{
    /// <summary>The virtual directory the embedded set sits in, so sibling lookups (children, eval cases) resolve inside it.</summary>
    private const string Root = "aw-recipes/sindri/";

    public async ValueTask<Result<EvalShippedResponse>> Handle(EvalShippedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var files = embedded.Files();
        var recipes = files.Where(static f => !f.RelativePath.Contains('/', StringComparison.Ordinal) && f.RelativePath.EndsWith(".yaml", StringComparison.Ordinal))
            .OrderBy(static f => f.RelativePath, StringComparer.Ordinal).ToList();
        var eval = new EvalRecipeService(reader, new RecipeSetFiles(files, Root), catalog, options);
        var results = new List<ShippedRecipeEval>(recipes.Count);
        foreach (var file in recipes)
        {
            results.Add(await Run(file, eval, cancellationToken).ConfigureAwait(false));
        }

        return new EvalShippedResponse(results.TrueForAll(static r => r.Passed), Empty: recipes.Count == 0, results);
    }

    private async Task<ShippedRecipeEval> Run(RecipeSetFile file, EvalRecipeService eval, CancellationToken cancellationToken)
    {
        var reading = reader.Read(Encoding.UTF8.GetString(file.Bytes.Span), file.RelativePath);
        var error = reading.Findings.FirstOrDefault(static f => f.Severity == Core.Workflow.Validation.FindingSeverity.Error);
        var id = Path.GetFileNameWithoutExtension(file.RelativePath);
        if (reading.Document is not { } document || error is not null)
        {
            return new ShippedRecipeEval(id, Version: null, Hash: null, DisplayName: null, Passed: false, [], error?.ToLine() ?? "it doesn't bind");
        }

        var recipe = document.Recipe;
        var result = await eval.Handle(new EvalRecipeRequest(Root + file.RelativePath), cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            return Of(recipe.Id.Value, document, [], result.Error.Message);
        }

        var cases = result.Value.Cases;
        return Of(recipe.Id.Value, document, cases, cases.Count == 0 ? "it declares no evals (fix: list evals/<recipe>/<case>.jsonl under evals:)" : null);
    }

    private static ShippedRecipeEval Of(string id, Core.Workflow.Validation.RecipeDocument document, IReadOnlyList<EvalCaseResult> cases, string? error) =>
        new(id, document.Recipe.Version, document.Hash.Value, document.Recipe.DisplayName, Passed: error is null && cases.All(static c => c.Passed), cases, error);
}
