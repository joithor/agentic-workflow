using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A group of sessions on the Agents page ("Outside a workflow", "Started by Sindri"): its rows, in the served order, expanded unless the human collapsed it.</summary>
/// <param name="name">The group's name.</param>
internal sealed class SessionGroupViewModel(string name) : ObservableObject
{
    /// <summary>The group's name.</summary>
    public string Name { get; } = name;

    /// <summary>The group's rows.</summary>
    public ObservableCollection<SessionRowViewModel> Rows { get; } = [];

    /// <summary>True while the rows show.</summary>
    public bool IsExpanded { get; private set => Set(ref field, value); } = true;

    /// <summary>The header: the name and the row count.</summary>
    public string Header => Labels.GroupHeader(Name, Rows.Count);

    /// <summary>The header button's name: the header and whether the rows show.</summary>
    public string ToggleName => Header + ", " + (IsExpanded ? Labels.GroupExpanded : Labels.GroupCollapsed);

    /// <summary>Collapses the group to its header, or shows its rows again.</summary>
    internal void Toggle()
    {
        IsExpanded = !IsExpanded;
        OnChanged(nameof(ToggleName));
    }

    /// <summary>Takes the rows as served, keeping the instances already shown.</summary>
    /// <param name="rows">The rows, in order.</param>
    internal void Show(IReadOnlyList<SessionRowViewModel> rows)
    {
        Patch.To(Rows, rows);
        OnChanged(nameof(Header));
        OnChanged(nameof(ToggleName));
    }
}
