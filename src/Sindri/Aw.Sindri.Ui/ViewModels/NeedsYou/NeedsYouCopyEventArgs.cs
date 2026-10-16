namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A key asks to copy a row's served command.</summary>
/// <param name="rowId">The row's id.</param>
/// <param name="text">The command.</param>
internal sealed class NeedsYouCopyEventArgs(string rowId, string text) : EventArgs
{
    /// <summary>The row's id.</summary>
    internal string RowId { get; } = rowId;

    /// <summary>The command, as served.</summary>
    internal string Text { get; } = text;
}
