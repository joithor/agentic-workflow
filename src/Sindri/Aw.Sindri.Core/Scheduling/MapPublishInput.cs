namespace Aw.Sindri.Core.Scheduling;

/// <summary>What the publish decision reads besides the clock.</summary>
/// <param name="Hold">The held draft.</param>
/// <param name="DraftVersion">The latest draft's number now; null: none.</param>
/// <param name="CurrentVersion">The current version's number; null: no map has been promoted yet.</param>
/// <param name="Outlooks">The outlook by repo name, for the hold's repos; a repo with no entry reads as absent.</param>
/// <param name="Wait">How long to wait for the index, counted from the hold's start (zero: do not wait).</param>
public sealed record MapPublishInput(MapHoldState Hold, int? DraftVersion, int? CurrentVersion, IReadOnlyDictionary<string, IndexOutlookDetail> Outlooks, TimeSpan Wait);
