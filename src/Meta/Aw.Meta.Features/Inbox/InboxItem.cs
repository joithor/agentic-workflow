using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.Inbox;

/// <summary>One inbound message as the inbox lists it.</summary>
/// <param name="Id">The message id.</param>
/// <param name="From">The sender's session name, or its socket when it had no name.</param>
/// <param name="Body">The message text.</param>
/// <param name="At">When it arrived.</param>
/// <param name="Kind">What the message is: Request, OwnerQuestion, Ready or LaneAnnounce.</param>
/// <param name="State">open, answered, dismissed or deferred.</param>
/// <param name="Until">When a deferred message reopens.</param>
public sealed record InboxItem(MessageId Id, string From, string Body, DateTimeOffset At, string Kind, string State, DateTimeOffset? Until);
