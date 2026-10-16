using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.LaneReply;

/// <summary>The LaneReply response.</summary>
/// <param name="Recorded">Whether a reply was recorded; false for a blank reply or when no team is set up.</param>
/// <param name="Id">The recorded message's id ("lane:&lt;lane&gt;:&lt;n&gt;"); null when none was recorded.</param>
public sealed record LaneReplyResponse(bool Recorded, MessageId? Id);
