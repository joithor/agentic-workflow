using Aw.Sindri.Ui.Platform;

#pragma warning disable MA0041, CA1822, MA0182 // red stubs (U3' T3)

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// Home's Needs you list (U3' T3): the rows as served, capped, with the overflow, selection that survives refreshes, the
/// hold while the pointer is over the panel, and the stalled and rebuilding banners. The app never sorts or filters the
/// rows; the pulse and seen state live in <see cref="Seen"/>.
/// </summary>
internal sealed class NeedsYouViewModel : ObservableObject
{
    internal NeedsYouViewModel(Preferences preferences, IMotionPreference motion, TimeProvider clock)
    {
        _ = (preferences, motion, clock);
        Seen = new NeedsYouSeenViewModel(preferences, motion, clock);
    }

    /// <summary>The pulse and seen state of the rows.</summary>
    internal NeedsYouSeenViewModel Seen { get; }

    /// <summary>The rows shown: the held-back ones left out, capped unless expanded.</summary>
    internal IReadOnlyList<NeedsYouRowViewModel> Rows => throw new NotSupportedException();

    /// <summary>How many rows the list has (the header's count), shown or not.</summary>
    internal int Count => throw new NotSupportedException();

    /// <summary>True when rows are left out by the cap.</summary>
    internal bool HasOverflow => throw new NotSupportedException();

    /// <summary>The overflow control's words ("all 7 →"); empty without overflow.</summary>
    internal string OverflowText => throw new NotSupportedException();

    /// <summary>True after <see cref="ShowAll"/>.</summary>
    internal bool IsExpanded => throw new NotSupportedException();

    /// <summary>The text scale; at 1.3 and over the cap drops to four.</summary>
    internal double TextScale { get; set; }

    /// <summary>True when the list has no rows and no banner.</summary>
    internal bool IsEmpty => throw new NotSupportedException();

    /// <summary>The served empty sentence.</summary>
    internal string EmptyText => throw new NotSupportedException();

    /// <summary>True while a stalled or rebuilding banner shows.</summary>
    internal bool HasBanner => throw new NotSupportedException();

    /// <summary>The banner's served sentence.</summary>
    internal string Banner => throw new NotSupportedException();

    /// <summary>The selected row's id; null for none.</summary>
    internal string? SelectedId => throw new NotSupportedException();

    /// <summary>The live region's line: "&lt;id&gt; cleared" for the last row to leave.</summary>
    internal string Announcement => throw new NotSupportedException();

    /// <summary>True while the pointer is over the panel: new rows are held back.</summary>
    internal bool PointerOver { get; set; }

    /// <summary>How many new rows are held back.</summary>
    internal int HeldCount => throw new NotSupportedException();

    /// <summary>The held rows' control ("2 new").</summary>
    internal string HeldText => throw new NotSupportedException();

    /// <summary>True when rows are held back.</summary>
    internal bool HasHeld => throw new NotSupportedException();

    /// <summary>Reads the seen ids and the reduce-motion switch.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal Task Restore(CancellationToken cancellationToken) => Seen.Restore(cancellationToken);

    /// <summary>Takes the list as the API serves it now.</summary>
    /// <param name="feed">The list.</param>
    internal void Update(NeedsYouFeed feed) => throw new NotSupportedException();

    /// <summary>Expands the list in place.</summary>
    internal void ShowAll() => throw new NotSupportedException();

    /// <summary>Shows the held rows now.</summary>
    internal void ShowHeld() => throw new NotSupportedException();

    /// <summary>Selects a row by id.</summary>
    /// <param name="id">The row's id.</param>
    internal void Select(string id) => throw new NotSupportedException();
}
