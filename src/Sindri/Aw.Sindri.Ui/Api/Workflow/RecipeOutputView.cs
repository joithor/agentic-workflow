namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A declared output of a recipe.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="From">From, as served.</param>
internal sealed record RecipeOutputView(
    string Key,
    string? Label,
    FactType Type,
    string From);
