using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A recipe state in path order (WF section 3.10).</summary>
/// <param name="State">The state id.</param>
/// <param name="Order">The position.</param>
/// <param name="BranchOf">The state it folds under.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeStateView(
    [property: JsonPropertyName("id")] string State,
    int Order,
    string? BranchOf);
