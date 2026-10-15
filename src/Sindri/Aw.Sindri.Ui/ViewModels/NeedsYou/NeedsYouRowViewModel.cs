namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>How one needs-you row looks as to urgency, seen state and motion (UI spec §5, screens spec R8-13).</summary>
internal sealed class NeedsYouRowViewModel : ObservableObject
{
    internal NeedsYouRowViewModel(NeedsYouEntry entry, bool seen, bool reducedMotion, bool arrived)
    {
        throw new NotSupportedException();
    }

    /// <summary>The row's id.</summary>
    public string Id { get; } = "";

    /// <summary>The row's tint.</summary>
    public RowTint Tint { get; } = RowTint.None;

    /// <summary>True when the tint pulses (an unseen pulse row, motion allowed).</summary>
    public bool IsPulsing { get; }

    /// <summary>True for the static 2 px attention outline (an unseen pulse row under reduced motion).</summary>
    public bool HasOutline { get; }

    /// <summary>The word "new" next to an unseen pulse row under reduced motion; empty otherwise.</summary>
    public string NewWord { get; } = "";

    /// <summary>True when the row slides in (it arrived after the list was first shown, and motion is allowed).</summary>
    public bool SlidesIn { get; }
}
