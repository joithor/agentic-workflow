namespace Aw.Sindri.Ui.Platform;

/// <summary>Which needs-you row the user picked in the tray's menu.</summary>
/// <param name="rowId">The row's id.</param>
public sealed class TrayRowEventArgs(string rowId) : EventArgs
{
    /// <summary>The row's id.</summary>
    public string RowId { get; } = rowId;
}
