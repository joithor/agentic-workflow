using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The children panel (screens §4.2): the child instances, grouped under the state that spawned them and, for a packet, by workstream.
/// Every figure and word is served; a child's frame opens by its id and its item by its id, and the panel sends nothing.
/// </summary>
internal sealed class ChildrenPanelViewModel : ObservableObject
{
    internal ChildrenPanelViewModel(InstanceDetail detail, IClipboard clipboard, TimeProvider clock, CatalogViewModel? catalog = null)
    {
        ChildRowViewModel Make(ChildRow served)
        {
            var row = new ChildRowViewModel(served, clipboard, clock, catalog);
            row.Opened += (_, e) => Opened?.Invoke(this, e);
            row.ItemRequested += (_, e) => ItemRequested?.Invoke(this, e);
            return row;
        }

        Groups = [.. detail.Children.Select(g => new ChildGroupViewModel(g, detail.Address, Make))];
        foreach (var group in Groups)
        {
            group.WorkRequested += (_, _) => WorkRequested?.Invoke(this, EventArgs.Empty);
        }

        Heading = Groups.Count > 0 ? Labels.ChildrenCount(Groups.Sum(g => g.Total)) : Labels.ChildrenHeading;
        Name = Labels.ChildrenName(detail.Address);
        EmptyText = detail.States.Any(s => s.Child) ? Labels.NoChildrenYet : Labels.NoChildren;
    }

    /// <summary>True while the disclosure is open; closed by default. The frame opens it when a key wants focus on a child.</summary>
    public bool IsOpen { get; set => Set(ref field, value); }

    /// <summary>Raised when a row asks to open its child's frame.</summary>
    internal event EventHandler<FrameOpenEventArgs>? Opened;

    /// <summary>Raised when a row asks to open its item's sheet.</summary>
    internal event EventHandler<ItemOpenEventArgs>? ItemRequested;

    /// <summary>Raised when a group asks to list every child in Work.</summary>
    internal event EventHandler? WorkRequested;

    /// <summary>The groups, in the order served.</summary>
    public IReadOnlyList<ChildGroupViewModel> Groups { get; }

    /// <summary>True when the recipe has children.</summary>
    public bool HasGroups => Groups.Count > 0;

    /// <summary>"CHILDREN 9".</summary>
    public string Heading { get; }

    /// <summary>The words when there are no children: "no children yet" when a state of the recipe marks children, else that the recipe declares none.</summary>
    public string EmptyText { get; }

    /// <summary>The panel's accessible name.</summary>
    public string Name { get; }

    /// <summary>Takes a re-read of the same panel: the ages move in place, the rows stay.</summary>
    /// <param name="detail">The instance as served now; its children's <see cref="Signature"/> equals this panel's.</param>
    internal void Update(InstanceDetail detail)
    {
        var rows = new Dictionary<string, ChildRowViewModel>(StringComparer.Ordinal);
        foreach (var row in Groups.SelectMany(g => g.AllRows))
        {
            _ = rows.TryAdd(row.Id, row);
        }

        foreach (var served in detail.Children.SelectMany(g => g.Workstreams.SelectMany(w => w.Rows).Concat(g.Rows)))
        {
            if (rows.TryGetValue(served.Id, out var row))
            {
                row.Update(served);
            }
        }
    }

    /// <summary>What the panel draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('\n', Groups.Select(g => g.Signature));
}
