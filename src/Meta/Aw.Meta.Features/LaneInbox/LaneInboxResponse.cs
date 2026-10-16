using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LaneInbox;

/// <summary>The LaneInbox response.</summary>
/// <param name="Messages">The messages not shown to the lane before; they are marked delivered.</param>
public sealed record LaneInboxResponse(IReadOnlyList<LaneMessage> Messages);
