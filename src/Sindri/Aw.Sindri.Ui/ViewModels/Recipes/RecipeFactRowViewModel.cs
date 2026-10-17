namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One declared input or output: a label line and a detail line, both as the recipe serves them.</summary>
internal sealed record RecipeFactRowViewModel(string Label, string Detail, string Generate)
{
    /// <summary>True when the recipe serves a generate template.</summary>
    public bool HasGenerate => Generate.Length > 0;

    /// <summary>The words screen readers hear: every visible line.</summary>
    public string Name => Label;
}
