namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The tint of a needs-you row (screens spec R8-13: no side stripes; the tint carries what the bar did).</summary>
internal enum RowTint
{
    /// <summary>No tint: a pulse row that has been seen.</summary>
    None,

    /// <summary>The attention tint, pulsing (an unseen pulse row, motion allowed).</summary>
    Pulsing,

    /// <summary>The attention tint, still: a failure row, or an unseen pulse row under reduced motion.</summary>
    Steady,

    /// <summary>A subtle ink tint: a calm row.</summary>
    Subtle,
}
