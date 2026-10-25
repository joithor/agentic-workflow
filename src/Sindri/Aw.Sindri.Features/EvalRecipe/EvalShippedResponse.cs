using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>What 'recipe eval --shipped' returns: every embedded recipe's evals; an empty embedded set passes.</summary>
/// <param name="Passed">Whether every recipe's evals passed (exit 0; otherwise 1).</param>
/// <param name="Empty">Whether the build embeds no recipes, so there was nothing to run.</param>
/// <param name="Recipes">Each embedded recipe, sorted by id (ordinal).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalShippedResponse(bool Passed, bool Empty, IReadOnlyList<ShippedRecipeEval> Recipes);
