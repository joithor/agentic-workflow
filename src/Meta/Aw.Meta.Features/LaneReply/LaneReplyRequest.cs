using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LaneReply;

/// <summary>The LaneReply request: a lane answers the meta.</summary>
/// <param name="Identity">The lane, from its environment.</param>
/// <param name="Text">The reply text.</param>
public sealed record LaneReplyRequest(LaneIdentity Identity, string Text);
