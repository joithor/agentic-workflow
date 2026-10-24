namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One match of a search over the map.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Kind">Its kind as served.</param>
/// <param name="Label">Its label.</param>
/// <param name="Area">The feature (area) it sits in; null when not known.</param>
internal sealed record MapSearchHit(string Key, string Kind, string Label, string? Area);
