#pragma warning disable CA1822, MA0041, IDE0060 // red stub: replaced by the green commit
namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page's keys (<see cref="WorkflowsViewModel"/>, continued; U8 T7).</summary>
internal sealed partial class WorkflowsViewModel
{
    /// <summary>The address of the row the keys act on; null for none.</summary>
    public string? SelectedAddress => null;

    /// <summary>The row the keys act on; null for none.</summary>
    public InstanceRowViewModel? SelectedRow => null;

    /// <summary>The words a key said when it only names a later phase; empty otherwise.</summary>
    public string KeyNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Selects a row by its address.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>True when a row of that address is shown.</returns>
    internal bool Select(string address) => false;

    /// <summary>Clears the selection.</summary>
    internal void Deselect()
    {
    }

    /// <summary>One single key on the page.</summary>
    /// <param name="key">J, K, Enter, A, X, B or N.</param>
    /// <returns>True when the key was used.</returns>
    internal bool Press(string key) => false;
}
