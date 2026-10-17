using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The recipe's states in the order the API serves them (screens spec §4.3): a branch folds under its source, a loop between neighbours is drawn between them, any other loop is a note on its state.</summary>
internal sealed class RecipeStatesViewModel
{
    internal RecipeStatesViewModel(RecipeView recipe)
    {
        OrderNote = recipe.OrderedBy == PathOrdering.TransitionList ? Labels.OrderedByTransitions : string.Empty;
    }

    /// <summary>The states in served order, branches folded under their source.</summary>
    public IReadOnlyList<RecipeStateNodeViewModel> Nodes { get; } = [];

    /// <summary>The header note when the recipe has no path; empty otherwise.</summary>
    public string OrderNote { get; }

    /// <summary>True when there is an order note.</summary>
    public bool HasOrderNote => OrderNote.Length > 0;

    /// <summary>The band's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.StatesHeading;
#pragma warning restore CA1822, MA0041
}
