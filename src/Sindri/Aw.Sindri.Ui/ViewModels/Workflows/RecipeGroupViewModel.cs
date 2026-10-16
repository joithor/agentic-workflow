using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One recipe's group on the Workflows list: its rows, and the columns its recipe's states draw.</summary>
internal sealed class RecipeGroupViewModel : ObservableObject
{
    /// <summary>The recipe's id.</summary>
    public string Recipe { get; } = string.Empty;

    /// <summary>How many rows the group holds.</summary>
    public int Count { get; }

    /// <summary>The group's rows, in the served order.</summary>
    public ObservableCollection<InstanceRowViewModel> Rows { get; } = [];

    /// <summary>The columns: the recipe's states in the served order.</summary>
    public IReadOnlyList<string> Columns { get; } = [];

    /// <summary>True while the rows show.</summary>
    public bool IsExpanded { get; }

    /// <summary>"show all" while collapsed; empty otherwise.</summary>
    public string ShowAllText { get; } = string.Empty;

    /// <summary>Collapses the group to its count, or shows its rows again.</summary>
    internal void Toggle() => throw new NotSupportedException();
}
