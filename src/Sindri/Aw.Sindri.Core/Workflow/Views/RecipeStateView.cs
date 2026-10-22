using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe state in path order (WF section 3.10).</summary>
/// <param name="State">The state id (wire name "id"); text, because a state id has no typed id yet.</param>
/// <param name="Order">The 1-based position on the main path; <see langword="null"/> for a branch state (WF section 3.10).</param>
/// <param name="BranchOf">The state it folds under.</param>
/// <param name="Why">The state's plain-words reason, or null (left out of the JSON).</param>
/// <param name="DisplayName">The plain name the app shows for the step instead of the state id, or null (left out of the JSON).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeStateView(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null);
