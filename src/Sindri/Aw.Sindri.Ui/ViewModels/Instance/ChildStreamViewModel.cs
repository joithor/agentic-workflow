#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823, IDE0032 // red stub: removed in the green commit
namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>A workstream of a children group, in the order served.</summary>
internal sealed class ChildStreamViewModel
{
    /// <summary>The stream's id.</summary>
    public string Stream => throw new NotSupportedException();

    /// <summary>The stream's label.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>"ws-1 UI".</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The stream's children.</summary>
    public IReadOnlyList<ChildRowViewModel> Rows => throw new NotSupportedException();
}
