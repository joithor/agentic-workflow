using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One transition of the recipe, as the app draws an edge.</summary>
/// <param name="From">The states it leaves.</param>
/// <param name="On">The trigger name ('step.completed').</param>
/// <param name="To">The state it enters.</param>
/// <param name="Outcome">The outcome its condition names, or null (left out of the JSON) when it names none.</param>
/// <param name="Label">The plain-words label, or null (left out of the JSON).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeTransitionView(
    IReadOnlyList<string> From,
    string On,
    string To,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Outcome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label = null);
