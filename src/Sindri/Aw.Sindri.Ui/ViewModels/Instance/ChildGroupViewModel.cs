#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823, IDE0032, CS0067 // red stub: removed in the green commit
namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The children one state spawned, of one recipe.</summary>
internal sealed class ChildGroupViewModel : ObservableObject
{
    /// <summary>Raised when the group asks to list every child in Work.</summary>
    internal event EventHandler? WorkRequested;

    /// <summary>The state that spawned the children.</summary>
    public string State => throw new NotSupportedException();

    /// <summary>The children's recipe.</summary>
    public string Recipe => throw new NotSupportedException();

    /// <summary>"build · workflow.step".</summary>
    public string Header => throw new NotSupportedException();

    /// <summary>"4 of 9 done".</summary>
    public string Counts => throw new NotSupportedException();

    /// <summary>The workstreams, in the order served; empty when the children are not grouped.</summary>
    public IReadOnlyList<ChildStreamViewModel> Streams => throw new NotSupportedException();

    /// <summary>True when the children are grouped by workstream.</summary>
    public bool HasStreams => throw new NotSupportedException();

    /// <summary>The children that sit in no workstream.</summary>
    public IReadOnlyList<ChildRowViewModel> Rows => throw new NotSupportedException();

    /// <summary>How many children are not listed.</summary>
    public int More => throw new NotSupportedException();

    /// <summary>True when some are not listed.</summary>
    public bool HasMore => throw new NotSupportedException();

    /// <summary>"… 4 more".</summary>
    public string MoreText => throw new NotSupportedException();

    /// <summary>The link's words.</summary>
    public string OpenAllText => throw new NotSupportedException();

    /// <summary>The link's accessible name.</summary>
    public string OpenAllName => throw new NotSupportedException();

    /// <summary>What the group draws, so a re-read of the same keeps it.</summary>
    internal string Signature => throw new NotSupportedException();

    /// <summary>Asks the frame to list every child in Work, unfiltered.</summary>
    public void OpenAllInWork() => throw new NotSupportedException();
}
