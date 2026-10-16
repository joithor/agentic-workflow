using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// How one needs-you row looks as to urgency, seen state and motion (UI spec §5; screens spec R8-13: the row's tint carries
/// what the side bar did). A pulse row is tinted and pulses until seen, then its tint clears; under reduced motion it keeps
/// a static outline and the word "new" until seen instead. A failure row keeps a steady tint, a calm row a subtle one.
/// </summary>
internal sealed class NeedsYouRowViewModel : ObservableObject
{
    private bool _seen;
    private bool _reduced;
    private bool _arrived;
    private readonly TimeProvider _clock;

    internal NeedsYouRowViewModel(NeedsYouEntry entry, bool seen, bool reducedMotion, bool arrived, TimeProvider clock)
    {
        Entry = entry;
        _clock = clock;
        (_seen, _reduced, _arrived) = (seen, reducedMotion, arrived);
    }

    /// <summary>The row's chip (D5: the chip, not the label, names its state).</summary>
    public NeedsYouChip Chip => Entry.Chip;

    /// <summary>The row's chip as the one chip control draws it.</summary>
#pragma warning disable MA0041, CA1822 // red stub
    public StateChip? StateChip => throw new NotSupportedException();
#pragma warning restore MA0041, CA1822

    /// <summary>The id shown at rest; empty when the row shows none.</summary>
    public string ShownId => Entry.ShownId;

    /// <summary>The row's title.</summary>
    public string Title => Entry.Title;

    /// <summary>The repo tag; empty for none.</summary>
    public string Repo => Entry.Repo;

    /// <summary>The served label, the secondary line.</summary>
    public string Reason => Entry.Reason;

    /// <summary>The one served command to copy; empty for none.</summary>
    public string Cli => Entry.Cli;

    /// <summary>How long the row has waited, as of the last update ("12m", "2h").</summary>
    public string Age => Entry.Since == default ? "" : Labels.RowAge(_clock.GetUtcNow() - Entry.Since);

    /// <summary>The name screen readers hear: the chip word and the id.</summary>
    public string AccessibleName
    {
        get
        {
            var id = Entry.ShownId.Length > 0 ? Entry.ShownId : Entry.Id;
            return Entry.Chip.Word.Length > 0 ? Entry.Chip.Word + " " + id : id;
        }
    }

    /// <summary>The row's controls, the primary first, then the others as served.</summary>
    public IReadOnlyList<NeedsYouActionControl> Controls
    {
        get
        {
            var primary = Entry.PrimaryAction;
            var first = Entry.Actions.Where(a => string.Equals(a.CatalogId, primary, StringComparison.Ordinal));
            var rest = Entry.Actions.Where(a => !string.Equals(a.CatalogId, primary, StringComparison.Ordinal));
            return
            [
                .. first.Select(a => new NeedsYouActionControl(a, IsPrimary: true, a.IsReady && !_rebuilding, Tooltip(a, _rebuilding))),
                .. rest.Select(a => new NeedsYouActionControl(a, IsPrimary: false, a.IsReady && !_rebuilding, Tooltip(a, _rebuilding))),
            ];
        }
    }

    /// <summary>Where Enter and a click on the title go: the row's own sheet, keyed on the row id (never the subject).</summary>
    public DeepLink SheetLink => new(DeepLinkKind.NeedsYou, Entry.Id);

    /// <summary>True when the row names an item, so it offers Open.</summary>
    public bool CanOpenItem => Entry.ItemId is not null;

    /// <summary>Where Open goes; null when the row names no item.</summary>
    public DeepLink? ItemLink => Entry.ItemId is { } item ? new DeepLink(DeepLinkKind.Item, item) : null;

    /// <summary>The served recorded-only sentence; empty when the row isn't recorded-only.</summary>
    public string Notice => Entry.RecordedOnly ? Entry.Notice : "";

    /// <summary>True when the row shows <see cref="Notice"/>.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>True while the views are stalled: the row stays, marked stale.</summary>
    public bool IsStale { get; private set; }

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>The served row, as of the last update.</summary>
    internal NeedsYouEntry Entry { get; private set; }

    /// <summary>Takes the row as the API serves it now; everything read from it is raised as changed.</summary>
    /// <param name="entry">The row.</param>
    internal void Replace(NeedsYouEntry entry)
    {
        if (!ReferenceEquals(entry, Entry))
        {
            Entry = entry;
            foreach (var name in _content)
            {
                OnChanged(name);
            }
        }
        else
        {
            OnChanged(nameof(Age));
        }
    }

    /// <summary>Sets what the list knows of the row: stale views, a rebuild in progress, and whether the keys act on it.</summary>
    /// <param name="stale">The views are stalled.</param>
    /// <param name="rebuilding">The views are rebuilding.</param>
    /// <param name="selected">The keys act on this row.</param>
    internal void SetContext(bool stale, bool rebuilding, bool selected)
    {
        if (stale != IsStale)
        {
            IsStale = stale;
            OnChanged(nameof(IsStale));
        }

        if (rebuilding != _rebuilding)
        {
            _rebuilding = rebuilding;
            OnChanged(nameof(Controls));
        }

        if (selected != IsSelected)
        {
            IsSelected = selected;
            OnChanged(nameof(IsSelected));
        }
    }

    private static readonly string[] _content =
    [
        nameof(Chip), nameof(ShownId), nameof(Title), nameof(Repo), nameof(Reason), nameof(Cli), nameof(Age), nameof(AccessibleName),
        nameof(Controls), nameof(CanOpenItem), nameof(ItemLink), nameof(Notice), nameof(HasNotice),
    ];

    private bool _rebuilding;

    private static string Tooltip(NeedsYouAction action, bool rebuilding) => action.Readiness switch
    {
        ActionReadiness.Ready when rebuilding => Labels.RetryAfterRebuild,
        ActionReadiness.CliOnly => Labels.ProtectedTooltip,
        ActionReadiness.Pending => action.Cli,
        _ => "",
    };

    /// <summary>The row's id.</summary>
    public string Id => Entry.Id;

    /// <summary>The row's tint.</summary>
    public RowTint Tint => Entry.Urgency switch
    {
        NeedsYouUrgency.Pulse => _seen ? RowTint.None : _reduced ? RowTint.Steady : RowTint.Pulsing,
        NeedsYouUrgency.Colour => RowTint.Steady,
        NeedsYouUrgency.Calm => RowTint.Subtle,
        _ => throw new InvalidOperationException("an urgency the app doesn't know"),
    };

    /// <summary>True when the tint pulses (an unseen pulse row, motion allowed).</summary>
    public bool IsPulsing => Tint == RowTint.Pulsing;

    /// <summary>True for the static 2 px attention outline (an unseen pulse row under reduced motion).</summary>
    public bool HasOutline => Entry.Urgency == NeedsYouUrgency.Pulse && !_seen && _reduced;

    /// <summary>The word "new" next to an unseen pulse row under reduced motion; empty otherwise.</summary>
    public string NewWord => HasOutline ? Labels.New : "";

    /// <summary>True when the row slides in (it arrived after the list was first shown, and motion is allowed).</summary>
    public bool SlidesIn => _arrived && !_reduced;

    /// <summary>Applies the panel's current state to the row, raising a change for each property that differs.</summary>
    /// <param name="seen">Whether the row has been seen.</param>
    /// <param name="reducedMotion">Whether motion is reduced.</param>
    /// <param name="arrived">Whether the row arrived in the latest update.</param>
    internal void Apply(bool seen, bool reducedMotion, bool arrived)
    {
        var before = (Tint, HasOutline, SlidesIn);
        (_seen, _reduced, _arrived) = (seen, reducedMotion, arrived);
        if (before.Tint != Tint)
        {
            OnChanged(nameof(Tint));
            OnChanged(nameof(IsPulsing));
        }

        if (before.HasOutline != HasOutline)
        {
            OnChanged(nameof(HasOutline));
            OnChanged(nameof(NewWord));
        }

        if (before.SlidesIn != SlidesIn)
        {
            OnChanged(nameof(SlidesIn));
        }
    }
}
