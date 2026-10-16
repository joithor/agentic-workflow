#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A text fact: one line and a Copy.</summary>
internal sealed class TextFactViewer : ObservableObject, IFactViewer
{
    internal TextFactViewer(FactRow row, FactViewerContext context) => throw new NotSupportedException();

    public FactRow Row => throw new NotSupportedException();

    /// <summary>The line: the fact's body once read, the row's summary until then.</summary>
    public string Line => throw new NotSupportedException();

    /// <summary>The Copy control's state.</summary>
    public CopyAction Copying => throw new NotSupportedException();

    /// <summary>Copy's accessible name.</summary>
    public string CopyName => throw new NotSupportedException();

    public Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Copies the line.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task Copy(CancellationToken cancellationToken) => throw new NotSupportedException();
}
