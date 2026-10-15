namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /workflow/recipes/{id}: what a recipe declares.</summary>
/// <param name="Inputs">Inputs, as served.</param>
/// <param name="Outputs">Outputs, as served.</param>
/// <param name="Urgency">Urgency, as served.</param>
/// <param name="OrderedBy">OrderedBy, as served.</param>
/// <param name="States">States, as served.</param>
/// <param name="Loops">Loops, as served.</param>
internal sealed record RecipeView(
    IReadOnlyList<RecipeInputView> Inputs,
    IReadOnlyList<RecipeOutputView> Outputs,
    RecipeUrgency Urgency,
    PathOrdering OrderedBy,
    IReadOnlyList<RecipeStateView> States,
    IReadOnlyList<RecipeLoopView> Loops);
