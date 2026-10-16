#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>One row of the facts panel.</summary>
internal sealed class FactRowViewModel : ObservableObject
{
    internal FactRowViewModel(FactRow row, string address, IReadOnlyList<StateCell> states, StatusCommand? complete, FactViewerRegistry registry, FactViewerContext context) => throw new NotSupportedException();

    /// <summary>The fact's key.</summary>
    public string Key => throw new NotSupportedException();

    /// <summary>The label as served.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>●, ◐ or ○.</summary>
    public string Glyph => throw new NotSupportedException();

    /// <summary>The status in words.</summary>
    public string StatusWords => throw new NotSupportedException();

    /// <summary>The summary as served.</summary>
    public string Summary => throw new NotSupportedException();

    /// <summary>Who produced it: "you" for a human, else as served; empty when none.</summary>
    public string By => throw new NotSupportedException();

    /// <summary>When, in the local time; empty when none.</summary>
    public string At => throw new NotSupportedException();

    /// <summary>"4 of 9"; empty when none.</summary>
    public string ProgressText => throw new NotSupportedException();

    /// <summary>The state it came from ("from scoping"); empty for an input.</summary>
    public string From => throw new NotSupportedException();

    /// <summary>The band's mark for the state it came from; empty for an input.</summary>
    public string FromMark => throw new NotSupportedException();

    /// <summary>True for a pending output the desk completes.</summary>
    public bool IsAwaiting => throw new NotSupportedException();

    /// <summary>The CLI that completes it, or empty.</summary>
    public string PendingCli => throw new NotSupportedException();

    /// <summary>The chip that says the form arrives later.</summary>
    public string PendingChip => throw new NotSupportedException();

    /// <summary>Copy of the pending CLI.</summary>
    public CopyAction CopyingCli => throw new NotSupportedException();

    /// <summary>True when the row can be viewed (anything produced or in progress).</summary>
    public bool CanView => throw new NotSupportedException();

    /// <summary>True while the viewer is open.</summary>
    public bool IsViewing => throw new NotSupportedException();

    /// <summary>The open viewer; null while closed.</summary>
    public IFactViewer? Viewer => throw new NotSupportedException();

    /// <summary>"View" or "Hide".</summary>
    public string ViewLabel => throw new NotSupportedException();

    /// <summary>The View button's accessible name.</summary>
    public string ViewName => throw new NotSupportedException();

    /// <summary>Opens or closes the viewer.</summary>
    public void ToggleView() => throw new NotSupportedException();

    /// <summary>Copies the pending CLI.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyCli(CancellationToken cancellationToken) => throw new NotSupportedException();
}
