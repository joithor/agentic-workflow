using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.SyncInbox;

/// <summary>The SyncInbox request.</summary>
public sealed record SyncInboxRequest(MessageId MessageId);
