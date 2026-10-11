using System.Globalization;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.ValidateRecipe;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>The 'recipe' verbs' text output (spec §6.3). Pure: RecipeCommandTable writes it.</summary>
internal static class RecipeText
{
    /// <summary>'recipe validate': one line per finding, then the hash when the recipe bound.</summary>
    /// <param name="response">The validation.</param>
    /// <returns>The text, each line ending in '\n'.</returns>
    internal static string Validation(ValidateRecipeResponse response)
    {
        var lines = response.Findings.Select(f => f.ToLine() + "\n");
        var hash = response.Hash is { } h ? $"hash {h.Value}\n" : string.Empty;
        return string.Concat(lines) + hash;
    }

    /// <summary>'recipe eval': one line per case, or the empty state.</summary>
    /// <param name="response">The eval results.</param>
    /// <returns>The text, each line ending in '\n'.</returns>
    internal static string Eval(EvalRecipeResponse response) =>
        response.Cases.Count == 0
            ? "no evals declared\n"
            : string.Concat(response.Cases.Select(c => c.Passed
                ? $"pass {c.Case}\n"
                : string.Create(CultureInfo.InvariantCulture, $"fail {c.Case}:{c.Line} {c.Detail}\n")));
}
