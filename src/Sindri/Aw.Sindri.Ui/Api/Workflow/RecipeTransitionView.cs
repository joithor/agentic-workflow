using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One transition of the recipe, as an edge.</summary>
/// <param name="From">The states it leaves.</param>
/// <param name="On">The trigger name.</param>
/// <param name="To">The state it enters.</param>
/// <param name="Outcome">The outcome its condition names; read as optional.</param>
/// <param name="Label">The plain-words label; read as optional.</param>
internal sealed record RecipeTransitionView(
    IReadOnlyList<string> From,
    string On,
    string To,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Outcome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label = null);
