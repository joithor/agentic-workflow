namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// How one needs-you row looks as to urgency, seen state and motion (UI spec §5; screens spec R8-13: the row's tint carries
/// what the side bar did). A pulse row is tinted and pulses until seen, then its tint clears; under reduced motion it keeps
/// a static outline and the word "new" until seen instead. A failure row keeps a steady tint, a calm row a subtle one.
/// </summary>
internal sealed class NeedsYouRowViewModel : ObservableObject
{
    private readonly NeedsYouEntry _entry;
    private bool _seen;
    private bool _reduced;
    private bool _arrived;

    internal NeedsYouRowViewModel(NeedsYouEntry entry, bool seen, bool reducedMotion, bool arrived)
    {
        _entry = entry;
        (_seen, _reduced, _arrived) = (seen, reducedMotion, arrived);
    }

    /// <summary>The row's id.</summary>
    public string Id => _entry.Id;

    /// <summary>The row's tint.</summary>
    public RowTint Tint => _entry.Urgency switch
    {
        NeedsYouUrgency.Pulse => _seen ? RowTint.None : _reduced ? RowTint.Steady : RowTint.Pulsing,
        NeedsYouUrgency.Colour => RowTint.Steady,
        NeedsYouUrgency.Calm => RowTint.Subtle,
        _ => throw new InvalidOperationException("an urgency the app doesn't know"),
    };

    /// <summary>True when the tint pulses (an unseen pulse row, motion allowed).</summary>
    public bool IsPulsing => Tint == RowTint.Pulsing;

    /// <summary>True for the static 2 px attention outline (an unseen pulse row under reduced motion).</summary>
    public bool HasOutline => _entry.Urgency == NeedsYouUrgency.Pulse && !_seen && _reduced;

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
