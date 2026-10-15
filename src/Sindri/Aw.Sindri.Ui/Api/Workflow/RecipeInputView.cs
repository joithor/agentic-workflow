namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A declared input of a recipe.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Required">Required, as served.</param>
internal sealed record RecipeInputView(
    string Key,
    string? Label,
    FactType Type,
    string? Kind,
    bool Required);
