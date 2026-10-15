namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

#pragma warning disable MA0041, CA1822, CS0067, MA0182 // red stubs (U3' T3 keys)

/// <summary>The Needs-you list's keys (U3' T3): J and K, A, X, Y, Enter and Esc.</summary>
internal sealed partial class NeedsYouViewModel
{
    /// <summary>How long keys are ignored after the selection moved on its own (a row cleared under the user's hand).</summary>
    internal static readonly TimeSpan KeyQuiet = TimeSpan.FromMilliseconds(500);

    /// <summary>Raised when a key asks to run an action; the shell runs it as a button press would.</summary>
    internal event EventHandler<NeedsYouActionEventArgs>? ActionRequested;

    /// <summary>Raised when a key asks to copy a row's command.</summary>
    internal event EventHandler<NeedsYouCopyEventArgs>? CopyRequested;

    /// <summary>Raised when Enter asks to open a row's sheet.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? SheetRequested;

    /// <summary>Raised when Esc closed the sheet: focus goes back to the row.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? FocusRequested;

    /// <summary>The row whose sheet is open; null for none.</summary>
    internal string? SheetRowId => throw new NotSupportedException();

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the list used it.</returns>
    internal bool Press(NeedsYouKey key) => throw new NotSupportedException();
}
