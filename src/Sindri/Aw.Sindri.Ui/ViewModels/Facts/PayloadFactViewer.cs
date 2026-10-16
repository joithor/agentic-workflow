#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823 // red stub: removed in the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>A payload fact (and every type with no viewer of its own): View reads the fact's body and shows its first three keys, then a tree.</summary>
internal sealed class PayloadFactViewer : ObservableObject, IFactViewer
{
    internal PayloadFactViewer(FactRow row, FactViewerContext context) => throw new NotSupportedException();

    public FactRow Row => throw new NotSupportedException();

    /// <summary>True until the body has been read.</summary>
    public bool IsLoading => throw new NotSupportedException();

    /// <summary>The served sentence when the body can't be shown; empty otherwise.</summary>
    public string ProblemText => throw new NotSupportedException();

    /// <summary>The first three keys, one line each.</summary>
    public IReadOnlyList<string> Preview => throw new NotSupportedException();

    /// <summary>The whole body as a tree.</summary>
    public IReadOnlyList<PayloadNodeViewModel> Tree => throw new NotSupportedException();

    /// <summary>"The engine cut this body short." when the body was truncated; empty otherwise.</summary>
    public string TruncatedNote => throw new NotSupportedException();

    public Task Load(CancellationToken cancellationToken) => throw new NotSupportedException();
}
