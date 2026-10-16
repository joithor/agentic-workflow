namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A declared loop of a recipe.</summary>
/// <param name="Name">Name, as served.</param>
/// <param name="From">From, as served.</param>
/// <param name="To">To, as served.</param>
/// <param name="Cap">Cap, as served.</param>
internal sealed record RecipeLoopView(
    string? Name,
    string From,
    string To,
    int Cap);
