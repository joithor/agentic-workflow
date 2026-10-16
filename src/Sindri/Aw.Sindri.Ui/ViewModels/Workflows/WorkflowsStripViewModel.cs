using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182, CA1801, IDE0060, CA1822, S1172, CA1070, MA0041, CS9113 // red stub
/// <summary>The strip.</summary>
internal sealed class WorkflowsStripViewModel(IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    internal const int Cap = 5;
    public IReadOnlyList<StripRowViewModel> Rows => throw new NotSupportedException();
    public bool HasMore => throw new NotSupportedException();
    public string MoreText => throw new NotSupportedException();
    public string Heading => throw new NotSupportedException();
    public string OpenText => throw new NotSupportedException();
    public double TextScale { get; set; }
    public bool IsEmpty => throw new NotSupportedException();
    public string EmptyText => throw new NotSupportedException();
    public bool IsNotServed => throw new NotSupportedException();
    public string NotServedText => throw new NotSupportedException();
    internal event EventHandler? WorkflowsRequested;
    internal void Update(InstanceList list) => throw new NotSupportedException();
    internal void SetNeedsYouShown(int count) => throw new NotSupportedException();
    internal void SetStale(bool stale) => throw new NotSupportedException();
    internal void MarkNotServed() => throw new NotSupportedException();
    internal void OpenWorkflows() => WorkflowsRequested?.Invoke(this, EventArgs.Empty);
}
#pragma warning restore MA0182, CA1801, IDE0060, CA1822, S1172, CA1070, MA0041, CS9113
