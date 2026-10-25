using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>One embedded recipe's eval results.</summary>
/// <param name="Recipe">The recipe id (wire name "id"); text, because a recipe that did not read has only its file name.</param>
/// <param name="Version">The recipe's version; null when the recipe did not read.</param>
/// <param name="Hash">The recipe hash; null when the recipe did not read.</param>
/// <param name="DisplayName">The recipe's display name, or null.</param>
/// <param name="Passed">Whether the recipe read and every case passed.</param>
/// <param name="Cases">Each case's result, in the recipe's order.</param>
/// <param name="Error">Why the recipe's evals could not run (it did not read, or an eval file is missing); null otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShippedRecipeEval(
    [property: JsonPropertyName("id")] string Recipe,
    int? Version,
    string? Hash,
    string? DisplayName,
    bool Passed,
    IReadOnlyList<EvalCaseResult> Cases,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error = null);
