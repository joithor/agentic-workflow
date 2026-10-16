using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.InboxDismiss;

/// <summary>The DismissInbox response.</summary>
/// <param name="Id">The dismissed message.</param>
public sealed record DismissInboxResponse(MessageId Id);
