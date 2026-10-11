using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A recipe's hash (spec §3.1): 'sha256:' over the RFC 8785 canonical JSON of the long form without 'hash'. Red stub (P1 unit (a)).</summary>
public static class RecipeHashing
{
    /// <summary>The hash of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The long form.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The hash.</returns>
    public static RecipeHash Of(Recipe recipe, JsonSerializerOptions options) => throw new NotSupportedException("red: the recipe hash lands with P1 unit (a)");
}
