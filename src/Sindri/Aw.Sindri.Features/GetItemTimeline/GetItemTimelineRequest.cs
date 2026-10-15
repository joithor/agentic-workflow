using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetItemTimeline;

/// <summary>An item's timeline after a cursor (GET /sindri/items/{itemId}/timeline?after=; API spec §3.1).</summary>
/// <param name="Item">The item, by id or alias.</param>
/// <param name="After">The sequence to read after, or null from the start.</param>
public sealed record GetItemTimelineRequest(ItemRef Item, long? After) : IQuery<TimelineResponse>;
