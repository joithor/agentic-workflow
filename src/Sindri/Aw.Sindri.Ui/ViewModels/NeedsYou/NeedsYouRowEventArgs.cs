namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A key asks for something about a row: its sheet, or focus back on it.</summary>
/// <param name="rowId">The row's id.</param>
internal sealed class NeedsYouRowEventArgs(string rowId) : EventArgs
{
    /// <summary>The row's id.</summary>
    internal string RowId { get; } = rowId;
}
