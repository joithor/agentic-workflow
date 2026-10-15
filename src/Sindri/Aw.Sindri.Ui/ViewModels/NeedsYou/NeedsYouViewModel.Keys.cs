namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The Needs-you list's keys (U3' T3): J and K move, A runs the primary, X turns a decision down, Y copies the command,
/// Enter opens the sheet and Esc closes it. The keys raise events; the shell runs the action as a button press would.
/// A key never runs a protected or pending action, and nothing is taken for <see cref="KeyQuiet"/> after the selection
/// moved on its own, so a row clearing under the user's hand can't turn the next key into an action on another row.
/// </summary>
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
    internal string? SheetRowId { get; private set; }

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the list used it.</returns>
    internal bool Press(NeedsYouKey key)
    {
        if (_clock.GetUtcNow() - _lastAutoMove < KeyQuiet)
        {
            return false;
        }

        var row = Seen.Rows.FirstOrDefault(r => string.Equals(r.Id, SelectedId, StringComparison.Ordinal));
        return key switch
        {
            NeedsYouKey.J => Move(1),
            NeedsYouKey.K => Move(-1),
            NeedsYouKey.Escape => CloseSheet(),
            _ when row is null => false,
            NeedsYouKey.A => RunPrimary(row),
            NeedsYouKey.X => RunReject(row),
            NeedsYouKey.Y => Copy(row),
            NeedsYouKey.Enter => OpenSheet(row),
            _ => false,
        };
    }

    private bool Move(int step)
    {
        var shown = Rows;
        if (shown.Count == 0)
        {
            return false;
        }

        var at = -1;
        for (var i = 0; i < shown.Count; i++)
        {
            if (string.Equals(shown[i].Id, SelectedId, StringComparison.Ordinal))
            {
                at = i;
            }
        }

        SetSelected(shown[at < 0 ? 0 : Math.Clamp(at + step, 0, shown.Count - 1)].Id);
        return true;
    }

    // A runs the row's primary control when the app may: ready, not behind a rebuild, and for a decision only with its sheet
    // open (the sheet shows what is being accepted). A protected or pending primary is never run.
    private bool RunPrimary(NeedsYouRowViewModel row)
    {
        var primary = row.Controls.FirstOrDefault(c => c.IsPrimary && c.IsEnabled);
        var decision = string.Equals(row.Entry.Subject?.Kind, "decision", StringComparison.Ordinal);
        return primary is not null && (!decision || string.Equals(SheetRowId, row.Id, StringComparison.Ordinal)) && Act(row, primary.Action);
    }

    // X picks the option the API marks rejectLike; a row without one has no X.
    private bool RunReject(NeedsYouRowViewModel row)
    {
        var reject = row.Controls.FirstOrDefault(c => c.Action.RejectLike && c.IsEnabled);
        return reject is not null && Act(row, reject.Action);
    }

    private bool Act(NeedsYouRowViewModel row, NeedsYouAction action)
    {
        Seen.Acted(row.Id);
        ActionRequested?.Invoke(this, new NeedsYouActionEventArgs(row.Id, action));
        return true;
    }

    private bool Copy(NeedsYouRowViewModel row)
    {
        if (row.Cli.Length == 0)
        {
            return false;
        }

        CopyRequested?.Invoke(this, new NeedsYouCopyEventArgs(row.Id, row.Cli));
        return true;
    }

    private bool OpenSheet(NeedsYouRowViewModel row)
    {
        SheetRowId = row.Id;
        SheetRequested?.Invoke(this, new NeedsYouRowEventArgs(row.Id));
        return true;
    }

    private bool CloseSheet()
    {
        if (SheetRowId is not { } open)
        {
            return false;
        }

        SheetRowId = null;
        FocusRequested?.Invoke(this, new NeedsYouRowEventArgs(open));
        return true;
    }
}
