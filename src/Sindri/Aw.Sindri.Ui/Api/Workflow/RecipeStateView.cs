using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A state of a recipe.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Order">Order, as served.</param>
/// <param name="BranchOf">BranchOf, as served.</param>
internal sealed record RecipeStateView(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf);
