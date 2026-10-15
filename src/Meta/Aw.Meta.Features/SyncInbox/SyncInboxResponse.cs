using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.SyncInbox;

/// <summary>The SyncInbox response.</summary>
/// <param name="NewMessages">The ids of the messages this sync recorded, in transcript order; each opens a reply obligation.</param>
public sealed record SyncInboxResponse(IReadOnlyList<MessageId> NewMessages);
