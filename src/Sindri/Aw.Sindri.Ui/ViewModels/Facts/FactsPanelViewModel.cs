using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>The facts panel (screens §4.2): every input and output the recipe declares, in the order served. A row whose viewer was open stays open across a re-read.</summary>
internal sealed class FactsPanelViewModel : ObservableObject
{
    internal FactsPanelViewModel(InstanceDetail detail, FactViewerRegistry registry, FactViewerContext context, IReadOnlySet<string>? open = null, Func<string, string>? nameOf = null)
    {
        var complete = detail.Current.Status.Commands.FirstOrDefault(c => c.Verb == StatusVerb.Complete);
        Rows = [.. detail.Facts.Select(f => new FactRowViewModel(f, detail.Address, detail.States, complete, registry, context, nameOf))];
        Name = Labels.FactsName(detail.Address);
        IsOpen = Rows.Any(r => r.IsAwaiting || r.IsViewing);   // an output that waits on the person is what they came for; the rest stays closed
        foreach (var row in Rows.Where(r => open?.Contains(r.Key) == true))
        {
            row.ToggleView();
            IsOpen = true;
        }
    }

    /// <summary>The rows.</summary>
    public IReadOnlyList<FactRowViewModel> Rows { get; }

    /// <summary>True when the recipe declares facts.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>True while the disclosure is open.</summary>
    public bool IsOpen { get; set => Set(ref field, value); }

    /// <summary>The panel's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.FactsHeading;

    /// <summary>Why the panel is there.</summary>
    public string Hint => Labels.FactsHint;

    /// <summary>The words when there are no facts.</summary>
    public string EmptyText => Labels.NoFacts;
#pragma warning restore CA1822, MA0041

    /// <summary>The panel's accessible name.</summary>
    public string Name { get; }

    /// <summary>The keys of the rows whose viewer is open.</summary>
    internal IReadOnlySet<string> OpenKeys => Rows.Where(r => r.IsViewing).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>What the panel draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('\n', Rows.Select(r => r.Signature));
}
