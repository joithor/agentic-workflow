namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>A recipe's eval results; no cases is the 'no evals declared' empty state.</summary>
/// <param name="Cases">Each case's result, in the recipe's order.</param>
/// <param name="Scaffold">The generated case's JSON lines, for <see cref="EvalMode.Scaffold"/>.</param>
/// <param name="Files">Every path's case, for <see cref="EvalMode.Scaffold"/> (a route recipe has one per path); the CLI's '--out' writes them. Not part of the JSON.</param>
public sealed record EvalRecipeResponse(IReadOnlyList<EvalCaseResult> Cases, string? Scaffold = null, [property: System.Text.Json.Serialization.JsonIgnore] IReadOnlyList<ScaffoldFile>? Files = null)
{
    /// <summary>Whether every case passed (exit 0; otherwise 1).</summary>
    public bool Passed => Cases.All(c => c.Passed);
}
