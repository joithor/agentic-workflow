using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One recipe's group on the Workflows list: its rows, and the columns its recipe's states draw (screens §4.2).</summary>
internal sealed class RecipeGroupViewModel(string recipe) : ObservableObject
{
    /// <summary>The recipe's id.</summary>
    public string Recipe { get; } = recipe;

    /// <summary>How many rows the group holds.</summary>
    public int Count => Rows.Count;

    /// <summary>The group's rows, in the served order.</summary>
    public ObservableCollection<InstanceRowViewModel> Rows { get; } = [];

    /// <summary>The columns: the recipe's states in the order the first row serves them, then any state only a later row has.</summary>
    public IReadOnlyList<string> Columns { get; private set => Set(ref field, value); } = [];

    /// <summary>True while the rows show; the human collapses a group and a re-read keeps it so.</summary>
    public bool IsExpanded { get; private set => Set(ref field, value); } = true;

    /// <summary>"show all" while collapsed; empty otherwise.</summary>
    public string ShowAllText => IsExpanded ? string.Empty : Labels.ShowAll;

    /// <summary>The group's header words: the recipe and its row count.</summary>
    public string Header => $"{Recipe} {Count}";

    /// <summary>The toggle's name for screen readers: the recipe, its count and whether the rows show.</summary>
    public string ToggleName => $"{Header}, {(IsExpanded ? Labels.GroupExpanded : Labels.GroupCollapsed)}";

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
        Columns = [.. rows.SelectMany(r => r.Stages.Select(s => s.State)).Distinct(StringComparer.Ordinal)];
        foreach (var row in rows)
        {
            row.Align(Columns);   // each cell under the head of its own state, a blank where the row has none
        }

        OnChanged(nameof(Count));
        OnChanged(nameof(Header));
        OnChanged(nameof(ToggleName));
    }
}
