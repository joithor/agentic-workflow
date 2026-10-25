namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One declared input or output: a label line, a detail line and (for an input) the generate template, all as the recipe serves them.</summary>
/// <param name="Label">The label, or the key when the recipe serves none.</param>
/// <param name="Detail">The key, type, kind or source state, and whether it is required.</param>
/// <param name="Generate">The generate line, or empty.</param>
internal sealed record RecipeFactRowViewModel(string Label, string Detail, string Generate)
{
    /// <summary>True when the recipe serves a generate template.</summary>
    public bool HasGenerate => Generate.Length > 0;
}
