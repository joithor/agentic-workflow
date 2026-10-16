using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus when the instance frame closes (<see cref="ShellWindow"/>, continued; UI spec §11.2): Esc and Back return it to the Workflows row that opened the frame.</summary>
internal sealed partial class ShellWindow
{
    // Where focus goes when the sheet closes: the strip row, the Workflows row or the queue row that opened it, the edge tab, else the row the selection moved to.
    private Control? SheetOpenerFrame() => OpenerStripFrame() ?? OpenerWorkflowFrame() ?? OpenerRowFrame() ?? Usable(_sheetOpener) ?? SelectedRowFrame();

    private Control? OpenerWorkflowFrame() => Shell?.OpenerWorkflow is { } address
        ? Usable(this.Get<Border>("Page").GetVisualDescendants().OfType<InstanceRow>()
            .FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.Address, address, StringComparison.Ordinal))
            ?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => string.Equals(b.Name, "WorkflowRowOpen", StringComparison.Ordinal)))
        : null;
}
