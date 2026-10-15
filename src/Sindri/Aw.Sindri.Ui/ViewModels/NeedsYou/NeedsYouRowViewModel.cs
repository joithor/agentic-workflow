#pragma warning disable IDE0052, MA0041, CA1822 // red stubs (U3' T3)

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
    public string Age => throw new NotSupportedException();

    /// <summary>The name screen readers hear: the chip word and the id.</summary>
    public string AccessibleName => throw new NotSupportedException();

    /// <summary>The row's controls, the primary first, then the others as served.</summary>
    public IReadOnlyList<NeedsYouActionControl> Controls => throw new NotSupportedException();

    /// <summary>Where Enter and a click on the title go: the row's own sheet, keyed on the row id (never the subject).</summary>
    public DeepLink SheetLink => throw new NotSupportedException();

    /// <summary>True when the row names an item, so it offers Open.</summary>
    public bool CanOpenItem => Entry.ItemId is not null;

    /// <summary>Where Open goes; null when the row names no item.</summary>
    public DeepLink? ItemLink => throw new NotSupportedException();

    /// <summary>The served recorded-only sentence; empty when the row isn't recorded-only.</summary>
    public string Notice => Entry.RecordedOnly ? Entry.Notice : "";

    /// <summary>True when the row shows <see cref="Notice"/>.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>True while the views are stalled: the row stays, marked stale.</summary>
    public bool IsStale { get; private set; }

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>The served row.</summary>
    internal NeedsYouEntry Entry { get; }

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
