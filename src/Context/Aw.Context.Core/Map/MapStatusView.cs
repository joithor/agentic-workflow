namespace Aw.Context.Core.Map;

/// <summary>What 'context map status' shows.</summary>
/// <param name="Version">The current version.</param>
/// <param name="Counts">Its counts (a session's: only the nodes its repo implements and what lies between them).</param>
/// <param name="Orphaned">Overrides whose target no longer resolves.</param>
/// <param name="Retained">How many earlier versions are kept.</param>
/// <param name="ScopedTo">The repo the counts are scoped to, or null for the whole map.</param>
public sealed record MapStatusView(MapVersion Version, MapCounts Counts, int Orphaned, int Retained, string? ScopedTo);
