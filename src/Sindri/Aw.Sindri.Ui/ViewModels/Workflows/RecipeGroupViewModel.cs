using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One recipe's group on the Workflows list: its rows, and the columns its recipe's states draw (screens §4.2).</summary>
internal sealed class RecipeGroupViewModel(string recipe) : ObservableObject
{
    /// <summary>Raised when the group's Recipe button asks to open the recipe's sheet.</summary>
    internal event EventHandler<RecipeRequestedEventArgs>? RecipeRequested;

    /// <summary>The recipe's id.</summary>
    public string Recipe { get; } = recipe;

    /// <summary>The workflow's name as a person reads it: the display name the group's first row serves, else the id made readable.</summary>
    public string Name { get; private set => Set(ref field, value); } = WorkflowNames.Of(recipe, displayName: null);

    /// <summary>How many rows the group holds.</summary>
    public int Count => Rows.Count;

    /// <summary>"1 run", "3 runs".</summary>
    public string CountText => Labels.RunCount(Count);

    /// <summary>The group's rows, in the served order.</summary>
    public ObservableCollection<InstanceRowViewModel> Rows { get; } = [];

    /// <summary>True while the rows show; the human collapses a group and a re-read keeps it so.</summary>
    public bool IsExpanded { get; private set => Set(ref field, value); } = true;

    /// <summary>"show all" while collapsed; empty otherwise.</summary>
    public string ShowAllText => IsExpanded ? string.Empty : Labels.ShowAll;

    /// <summary>The group's header words: the workflow's name and its run count.</summary>
    public string Header => $"{Name} · {CountText}";

    /// <summary>The toggle's name for screen readers: the recipe, its count and whether the rows show.</summary>
    public string ToggleName => $"{Header}, {(IsExpanded ? Labels.GroupExpanded : Labels.GroupCollapsed)}";

    /// <summary>The Recipe button's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string RecipeLinkText => Labels.RecipeLink;
#pragma warning restore CA1822, MA0041

    /// <summary>The Recipe button's accessible name.</summary>
    public string RecipeLinkName => Labels.OpenRecipeName(Name);

    /// <summary>Opens the recipe's approved version.</summary>
    internal void OpenRecipe() => RecipeRequested?.Invoke(this, new RecipeRequestedEventArgs(Recipe));

    /// <summary>Collapses the group to its count, or shows its rows again.</summary>
    internal void Toggle()
    {
        IsExpanded = !IsExpanded;
        OnChanged(nameof(ShowAllText));
        OnChanged(nameof(ToggleName));
    }

    /// <summary>Takes the rows as served.</summary>
    /// <param name="rows">The group's rows, in order.</param>
    internal void Show(IReadOnlyList<InstanceRowViewModel> rows)
    {
        Patch.To(Rows, rows);
        Name = rows.Count > 0 ? rows[0].DisplayName : Name;
        OnChanged(nameof(Count));
        OnChanged(nameof(CountText));
        OnChanged(nameof(Header));
        OnChanged(nameof(RecipeLinkName));
        OnChanged(nameof(ToggleName));
    }
}
