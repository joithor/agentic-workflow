using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A loop of a recipe (WF section 3.10).</summary>
/// <param name="Name">The loop name.</param>
/// <param name="From">The from state.</param>
/// <param name="To">The to state.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeLoopView(
    string? Name,
    string From,
    string To);
