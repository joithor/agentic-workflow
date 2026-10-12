namespace Aw.Sindri.Core.Views;

/// <summary>What applying one page did.</summary>
/// <param name="State">The state after the page.</param>
/// <param name="Stall">The event the page stopped at, or null when every event applied.</param>
public sealed record PageApplied(ProjectorState State, ViewStall? Stall);
