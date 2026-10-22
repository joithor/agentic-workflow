using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A state of a recipe.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Order">Order, as served.</param>
/// <param name="BranchOf">BranchOf, as served.</param>
/// <param name="Why">Why the state matters, in a sentence; read as optional (an older Sindri serves none).</param>
/// <param name="DisplayName">The state's name in words; read as optional (an older Sindri serves none).</param>
/// <param name="Kind">Who does the state; read as optional (an older Sindri serves none, and so does a state of unknown kind).</param>
/// <param name="OnOutcome">What each outcome of a desk state means, in the step's order; read as optional.</param>
/// <param name="Inputs">The facts the state's step reads; read as optional.</param>
/// <param name="Outputs">The facts the state's step writes; read as optional.</param>
internal sealed record RecipeStateView(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecipeStateKind? Kind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeOutcomeText>? OnOutcome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeFactView>? Inputs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<RecipeFactView>? Outputs = null);
