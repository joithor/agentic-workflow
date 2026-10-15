using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetItemTimeline;

/// <summary>A page of an item's timeline (U2-API-4): entries '{sequence, time, type, actor, line}'.</summary>
/// <param name="Entries">The lines, oldest first.</param>
/// <param name="Next">The cursor for the next page, or null on the last page.</param>
/// <param name="Empty">The timeline's sentence when it has no line (or the views are stale), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record TimelineResponse(IReadOnlyList<TimelineRow> Entries, long? Next, string? Empty, AsOf AsOf);
