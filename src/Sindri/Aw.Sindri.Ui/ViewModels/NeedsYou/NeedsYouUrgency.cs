namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>How urgently the API asks for a needs-you row (UI spec §5): the app only maps it to a look.</summary>
internal enum NeedsYouUrgency
{
    /// <summary>Decisions, parks and escalations: tinted, pulsing until seen.</summary>
    Pulse,

    /// <summary>Failures: a steady tint, no motion.</summary>
    Colour,

    /// <summary>Profile drift: a subtle tint, counted but not in colour.</summary>
    Calm,
}
