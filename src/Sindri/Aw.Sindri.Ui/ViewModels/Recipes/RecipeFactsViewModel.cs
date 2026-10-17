using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>What the recipe declares as inputs and outputs, in declared order (screens spec §4.3).</summary>
internal sealed class RecipeFactsViewModel
{
    internal RecipeFactsViewModel(RecipeView recipe)
    {
        NoOutputsText = recipe.Id;
    }

    /// <summary>The inputs.</summary>
    public IReadOnlyList<RecipeFactRowViewModel> Inputs { get; } = [];

    /// <summary>The outputs.</summary>
    public IReadOnlyList<RecipeFactRowViewModel> Outputs { get; } = [];

    /// <summary>True when the recipe declares outputs.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>What the outputs section says when there are none.</summary>
    public string NoOutputsText { get; }
}
