namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>How one action of a row is drawn: the action, whether it is the row's primary, whether it takes input, and its tooltip.</summary>
/// <param name="Action">The served action.</param>
/// <param name="IsPrimary">True for the row's primary control (drawn first).</param>
/// <param name="IsEnabled">True when it takes input: ready, and the views aren't rebuilding.</param>
/// <param name="Tooltip">Why it can't be used (the protected sentence, the served command of a pending action, retry after the rebuild); empty when it can.</param>
internal sealed record NeedsYouActionControl(NeedsYouAction Action, bool IsPrimary, bool IsEnabled, string Tooltip)
{
    /// <summary>True for a protected action: the view shows the lock and its command, and nothing here runs it.</summary>
    internal bool IsProtected => Action.IsProtected;
}
