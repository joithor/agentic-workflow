using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.InboxDismiss;

/// <summary>The DismissInbox request: close a reply obligation without replying.</summary>
/// <param name="Id">The message.</param>
/// <param name="Reason">Why no reply is needed.</param>
public sealed record DismissInboxRequest(MessageId Id, string Reason);
