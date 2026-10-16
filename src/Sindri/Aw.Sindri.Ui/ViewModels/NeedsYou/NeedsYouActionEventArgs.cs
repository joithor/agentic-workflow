namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A key asks to run an action of a row.</summary>
/// <param name="rowId">The row's id.</param>
/// <param name="action">The action; for a decision, the option.</param>
internal sealed class NeedsYouActionEventArgs(string rowId, NeedsYouAction action) : EventArgs
{
    /// <summary>The row's id.</summary>
    internal string RowId { get; } = rowId;

    /// <summary>The action.</summary>
    internal NeedsYouAction Action { get; } = action;
}
