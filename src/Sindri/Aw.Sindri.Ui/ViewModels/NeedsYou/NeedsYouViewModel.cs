using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

#pragma warning disable MA0182 // until Home places the list (U3' T1 maps the API's rows onto it)

/// <summary>
/// Home's Needs you list (U3' T3): the rows as served, capped, with the overflow, selection that survives refreshes, the
/// hold while the pointer is over the panel, and the stalled and rebuilding banners. The app never sorts or filters the
/// rows; the pulse and seen state live in <see cref="Seen"/>. The keys are in the other half of this class.
/// </summary>
internal sealed partial class NeedsYouViewModel : ObservableObject
{
    /// <summary>The most rows Home shows before the overflow control (UI spec §3.2).</summary>
    internal const int Cap = 5;

    /// <summary>The cap at a text scale of <see cref="LargeTextScale"/> and over.</summary>
    internal const int LargeTextCap = 4;

    /// <summary>The text scale from which the cap drops.</summary>
    internal const double LargeTextScale = 1.3;

    private static readonly string[] _listProperties =
    [
        nameof(Rows), nameof(Count), nameof(HasOverflow), nameof(OverflowText), nameof(IsExpanded), nameof(IsEmpty), nameof(EmptyText),
        nameof(HasBanner), nameof(Banner), nameof(HeldCount), nameof(HeldText), nameof(HasHeld), nameof(SheetRow),
    ];

    private NeedsYouFeed _feed = new([], "");
    private readonly TimeProvider _clock;
    private DateTimeOffset _lastAutoMove = DateTimeOffset.MinValue;
    private List<NeedsYouEntry> _visible = [];
    private HashSet<string> _shownIds = new(StringComparer.Ordinal);
    private bool _hasShown;

    internal NeedsYouViewModel(Preferences preferences, IMotionPreference motion, TimeProvider clock)
    {
        _clock = clock;
        Seen = new NeedsYouSeenViewModel(preferences, motion, clock);
        Seen.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(NeedsYouSeenViewModel.Rows), StringComparison.Ordinal))
            {
                Changed(_listProperties);
            }
        };
    }

    /// <summary>The pulse and seen state of the rows.</summary>
    internal NeedsYouSeenViewModel Seen { get; }

    /// <summary>The rows shown: the held-back ones left out, capped unless expanded.</summary>
    internal IReadOnlyList<NeedsYouRowViewModel> Rows => IsExpanded ? Seen.Rows : [.. Seen.Rows.Take(CurrentCap)];

    /// <summary>How many rows the list has (the header's count), shown or not; the held-back ones are not counted.</summary>
    internal int Count => Seen.Rows.Count;

    /// <summary>True when the cap leaves rows out.</summary>
    internal bool HasOverflow => !IsExpanded && Count > CurrentCap;

    /// <summary>The overflow control's words ("all 7 →"); empty without overflow.</summary>
    internal string OverflowText => HasOverflow ? Labels.AllRows(Count) : "";

    /// <summary>True after <see cref="ShowAll"/>.</summary>
    internal bool IsExpanded { get; private set; }

    /// <summary>The text scale; at 1.3 and over the cap drops to four.</summary>
    internal double TextScale
    {
        get;
        set
        {
            field = value;
            Changed(_listProperties);
        }
    } = 1.0;

    /// <summary>True when the list has no rows and no banner.</summary>
    internal bool IsEmpty => Count == 0 && !HasBanner;

    /// <summary>The served empty sentence; empty unless the list is empty.</summary>
    internal string EmptyText => IsEmpty ? _feed.Sentence : "";

    /// <summary>True while a stalled or rebuilding banner shows.</summary>
    internal bool HasBanner => _feed.Stalled || _feed.Rebuilding;

    /// <summary>The banner's served sentence; empty without a banner.</summary>
    internal string Banner => HasBanner ? _feed.Sentence : "";

    /// <summary>The row whose sheet is open; null for none.</summary>
    internal NeedsYouRowViewModel? SheetRow => SheetRowId is { } id ? Seen.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal)) : null;

    /// <summary>The selected row's id; null for none.</summary>
    internal string? SelectedId { get; private set; }

    /// <summary>The live region's line: "&lt;id&gt; cleared" for the last row to leave.</summary>
    internal string Announcement { get; private set; } = "";

    /// <summary>True while the pointer is over the panel: new rows are held back.</summary>
    internal bool PointerOver
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            Apply();
        }
    }

    /// <summary>How many new rows are held back.</summary>
    internal int HeldCount => _feed.Rows.Count - _visible.Count;

    /// <summary>The held rows' control ("2 new"); empty for none.</summary>
    internal string HeldText => HasHeld ? Labels.NewRows(HeldCount) : "";

    /// <summary>True when rows are held back.</summary>
    internal bool HasHeld => HeldCount > 0;

    private int CurrentCap => TextScale >= LargeTextScale ? LargeTextCap : Cap;

    /// <summary>Reads the seen ids and the reduce-motion switch.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal Task Restore(CancellationToken cancellationToken) => Seen.Restore(cancellationToken);

    /// <summary>
    /// Takes the list as the API serves it now. A selected row that left is replaced by the one after it (or before, at the
    /// end) and the live region says so.
    /// </summary>
    /// <param name="feed">The list.</param>
    internal void Update(NeedsYouFeed feed)
    {
        _feed = feed;
        Apply();
    }

    /// <summary>Expands the list in place.</summary>
    internal void ShowAll()
    {
        IsExpanded = true;
        Changed(_listProperties);
    }

    /// <summary>Shows the held rows now.</summary>
    internal void ShowHeld()
    {
        _shownIds.UnionWith(_feed.Rows.Select(r => r.Id));
        Apply();
    }

    /// <summary>Selects a row by id; an id that isn't in the list selects nothing.</summary>
    /// <param name="id">The row's id.</param>
    internal void Select(string id)
    {
        if (Seen.Rows.Any(r => string.Equals(r.Id, id, StringComparison.Ordinal)))
        {
            SetSelected(id);
        }
    }

    /// <summary>Selects no row (the keys moved on to another list).</summary>
    internal void Deselect() => SetSelected(id: null);

    /// <summary>Marks every current row seen.</summary>
    internal void MarkAllSeen() => Seen.MarkAllSeen();

    private void Apply()
    {
        var before = Rows.Select(r => r.Entry).ToList();
        var holding = PointerOver && _hasShown;
        _visible = [.. _feed.Rows.Where(e => !holding || _shownIds.Contains(e.Id))];
        _shownIds = [.. _visible.Select(e => e.Id)];
        _hasShown = true;
        Seen.Update(_visible);
        var selected = SelectedId;
        if (selected is not null && !_visible.Exists(e => string.Equals(e.Id, selected, StringComparison.Ordinal)))
        {
            Clear(selected, before);
        }

        foreach (var row in Seen.Rows)
        {
            row.SetContext(_feed.Stalled, _feed.Rebuilding, string.Equals(row.Id, SelectedId, StringComparison.Ordinal));
        }

        Changed(_listProperties);
    }

    // The selected row left the list: select the row after it that remains (else the one before), and say it cleared.
    private void Clear(string id, List<NeedsYouEntry> before)
    {
        var at = before.FindIndex(e => string.Equals(e.Id, id, StringComparison.Ordinal));
        var kept = _visible.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var shown = at >= 0 ? before[at] : null;
        var next = at < 0 ? null : before.Skip(at + 1).Concat(before.Take(Math.Max(at, 0)).Reverse()).FirstOrDefault(e => kept.Contains(e.Id));
        SelectedId = next?.Id;
        if (shown is not null)
        {
            Announcement = Labels.Cleared(shown.ShownId.Length > 0 ? shown.ShownId : shown.Id);
            OnChanged(nameof(Announcement));
        }

        if (next is not null)
        {
            _lastAutoMove = _clock.GetUtcNow();
        }

        OnChanged(nameof(SelectedId));
    }

    private void SetSelected(string? id)
    {
        if (string.Equals(id, SelectedId, StringComparison.Ordinal))
        {
            return;
        }

        SelectedId = id;
        foreach (var row in Seen.Rows)
        {
            row.SetContext(_feed.Stalled, _feed.Rebuilding, string.Equals(row.Id, id, StringComparison.Ordinal));
        }

        OnChanged(nameof(SelectedId));
    }

    private void Changed(string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }
}
