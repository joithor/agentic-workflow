using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe state in path order (WF section 3.10).</summary>
/// <param name="State">The state id (wire name "id"); text, because a state id has no typed id yet.</param>
/// <param name="Order">The 1-based position on the main path; <see langword="null"/> for a branch state (WF section 3.10).</param>
/// <param name="BranchOf">The state it folds under.</param>
/// <param name="Why">The state's plain-words reason, or null (left out of the JSON).</param>
/// <param name="DisplayName">The plain name the app shows for the step instead of the state id, or null (left out of the JSON).</param>
/// <param name="Kind">Who does the state; null (left out of the JSON) when it is unknown, a terminal state for one.</param>
/// <param name="OnOutcome">What each outcome of a desk state means, in the step's outcome order; null (left out of the JSON) when the recipe writes none.</param>
/// <param name="Inputs">The facts the state's step reads; null (left out of the JSON) when there are none.</param>
/// <param name="Outputs">The facts the state's step writes; null (left out of the JSON) when there are none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeStateView(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecipeStateKind? Kind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeOutcomeText>? OnOutcome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeFactView>? Inputs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeFactView>? Outputs = null);
