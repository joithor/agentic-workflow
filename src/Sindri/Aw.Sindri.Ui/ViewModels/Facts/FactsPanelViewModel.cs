#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>The facts panel (screens §4.2): every input and output the recipe declares, in the order served.</summary>
internal sealed class FactsPanelViewModel : ObservableObject
{
    internal FactsPanelViewModel(InstanceDetail detail, FactViewerRegistry registry, FactViewerContext context, IReadOnlySet<string>? open = null) => throw new NotSupportedException();

    /// <summary>The rows.</summary>
    public IReadOnlyList<FactRowViewModel> Rows => throw new NotSupportedException();

    /// <summary>True when the recipe declares facts.</summary>
    public bool HasRows => throw new NotSupportedException();

    /// <summary>The panel's heading.</summary>
    public string Heading => throw new NotSupportedException();

    /// <summary>The panel's accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>The words when there are no facts.</summary>
    public string EmptyText => throw new NotSupportedException();

    /// <summary>The keys of the rows whose viewer is open.</summary>
    internal IReadOnlySet<string> OpenKeys => throw new NotSupportedException();

    /// <summary>What the panel draws, so a re-read of the same keeps it.</summary>
    internal string Signature => throw new NotSupportedException();
}
