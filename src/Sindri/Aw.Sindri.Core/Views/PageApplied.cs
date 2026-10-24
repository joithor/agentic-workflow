namespace Aw.Sindri.Core.Views;

/// <summary>What applying one page did.</summary>
/// <param name="State">The state after the page.</param>
/// <param name="Stall">The event the page stopped at, or null when every event applied.</param>
/// <param name="Unreadable">How many stored rows the page's folds couldn't read back (written under an older row shape) and treated as absent; the projector rebuilds the views when it is above 0.</param>
public sealed record PageApplied(ProjectorState State, ViewStall? Stall, int Unreadable = 0);
