namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>The one reading of the command catalog's "ui" word ("ready", "pending:&lt;phase&gt;", "cli-only:&lt;reason&gt;"): a word this build does not know is cli-only, because that never runs.</summary>
internal static class CatalogReadiness
{
    private const string PendingPrefix = "pending:";

    /// <summary>Reads a catalog row's ui word.</summary>
    /// <param name="ui">The word as served.</param>
    /// <returns>How the control is drawn and, for a pending one, the phase that brings it.</returns>
    internal static (ControlKind Kind, string Stage) Parse(string ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        return ui switch
        {
            "ready" => (ControlKind.Ready, string.Empty),
            _ when ui.StartsWith(PendingPrefix, StringComparison.Ordinal) => (ControlKind.Pending, ui[PendingPrefix.Length..]),
            _ => (ControlKind.CliOnly, string.Empty),
        };
    }
}
