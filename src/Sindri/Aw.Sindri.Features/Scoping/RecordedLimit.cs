namespace Aw.Sindri.Features.Scoping;

/// <summary>One row of the scoping spec's Recorded limits table (§8.2): what a coverage source must say to be a recorded limit, and what the stop tells the owner.</summary>
/// <param name="Name">The limit's name, as limits[].why carries it.</param>
/// <param name="Lens">The one lens it belongs to, or null for any lens.</param>
/// <param name="State">The source state it matches: 'unavailable' or 'not-configured'.</param>
/// <param name="Reason">The packet's own reason key (the gather step writes it).</param>
/// <param name="Label">What the row names as the limit ('never synced' for not-synced).</param>
/// <param name="Fix">The fix line.</param>
internal sealed record RecordedLimit(string Name, string? Lens, string State, string Reason, string Label, string Fix);
