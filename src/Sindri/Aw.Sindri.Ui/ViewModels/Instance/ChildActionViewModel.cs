#pragma warning disable CA1822, MA0041, IDE0021, MA0182, IDE0060, CA1801, CA1812, IDE0051, CA1823, IDE0032 // red stub: removed in the green commit
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One action a child row serves, other than opening its item: shown, never sent from here (the send is U8b). A grant or an accept is the lock button with its popover.</summary>
internal sealed class ChildActionViewModel
{
    /// <summary>The action's words.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>The control's accessible name: the words and the child.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>The lock button for a protected action; null for the others.</summary>
    public ProtectedActionViewModel? Protected => throw new NotSupportedException();

    /// <summary>The chip that says the send arrives later; empty for a protected action.</summary>
    public string PendingChip => throw new NotSupportedException();
}
