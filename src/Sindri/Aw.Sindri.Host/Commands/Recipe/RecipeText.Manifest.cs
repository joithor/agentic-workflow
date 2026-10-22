using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.EvalRecipe;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>The 'recipe manifest' and 'recipe eval --shipped' text output (setup-once spec §5.3). Pure: RecipeCommandTable writes it.</summary>
internal static partial class RecipeText
{
    /// <summary>
    /// One line per recipe, sorted by id: 'workflow.build@3 → sha256:… (3 eval cases)'; a line 'invalid &lt;file&gt;: &lt;finding&gt;' for each recipe that did not read; then 'digest sha256:…' when every recipe read.
    /// The publish compares this text with the last publish's, so it is stable for a stable set.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>The text, each line ending in '\n'.</returns>
    internal static string Manifest(RecipeManifest manifest)
    {
        var text = new StringBuilder();
        foreach (var r in manifest.Recipes)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{r.Id}@{r.Version} → {r.Hash} ({r.EvalCases} eval {(r.EvalCases == 1 ? "case" : "cases")})\n");
        }

        foreach (var bad in manifest.Invalid)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"invalid {bad.File}: {bad.Finding}\n");
        }

        return manifest.Digest is { } digest ? text.Append("digest ").Append(digest).Append('\n').ToString() : text.ToString();
    }

    /// <summary>
    /// 'recipe eval --shipped': per recipe its 'id@version' line, then one indented line per case (or its error), then one summary line: '26 recipes passed (80 cases)', or
    /// '1 case failed in 1 recipe'. An empty embedded set says so.
    /// </summary>
    /// <param name="response">The results.</param>
    /// <returns>The text, each line ending in '\n'.</returns>
    internal static string EvalShipped(EvalShippedResponse response)
    {
        if (response.Empty)
        {
            return "no recipes are embedded in this build; nothing to evaluate\n";
        }

        var text = new StringBuilder();
        foreach (var r in response.Recipes)
        {
            _ = text.Append(r.Recipe).Append(r.Version is { } version ? string.Create(CultureInfo.InvariantCulture, $"@{version}") : string.Empty).Append('\n');
            foreach (var line in r.Cases.Select(static c => c.ToLine()).Concat(r.Error is null ? [] : ["error: " + r.Error]))
            {
                _ = text.Append("  ").Append(line).Append('\n');
            }
        }

        var failed = response.Recipes.Sum(static r => r.Cases.Count(static c => !c.Passed) + (r.Error is null ? 0 : 1));
        var failedRecipes = response.Recipes.Count(static r => !r.Passed);
        var cases = response.Recipes.Sum(static r => r.Cases.Count);
        return text.Append(response.Passed
            ? $"{Plural(response.Recipes.Count, "recipe")} passed ({Plural(cases, "case")})\n"
            : $"{Plural(failed, "case")} failed in {Plural(failedRecipes, "recipe")}\n").ToString();
    }

    private static string Plural(int count, string noun) => string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
}
