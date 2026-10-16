using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182, CA1801, IDE0060, CA1822, S1172, MA0041, CS9113 // red stub
/// <summary>One line of the strip.</summary>
internal sealed class StripRowViewModel(InstanceSummary row, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    public StateChip Chip => throw new NotSupportedException();
    public string Subject => throw new NotSupportedException();
    public string Recipe => throw new NotSupportedException();
    public string Stage => throw new NotSupportedException();
    public string Now => throw new NotSupportedException();
    public string Age => throw new NotSupportedException();
    public string Badge => throw new NotSupportedException();
    public string Glyph => throw new NotSupportedException();
    public bool HasBadge => throw new NotSupportedException();
    public bool CanAnswer => throw new NotSupportedException();
    public string AnswerText => throw new NotSupportedException();
    public string AnswerName => throw new NotSupportedException();
    public ProtectedActionViewModel? Protected => throw new NotSupportedException();
    public string Address => throw new NotSupportedException();
    public string AccessibleName => throw new NotSupportedException();
    public bool IsStale => throw new NotSupportedException();
}
#pragma warning restore MA0182, CA1801, IDE0060, CA1822, S1172, MA0041, CS9113
