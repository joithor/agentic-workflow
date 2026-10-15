using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.InboxDefer;

/// <summary>The DeferInbox response.</summary>
/// <param name="Id">The deferred message.</param>
/// <param name="Until">When it reopens.</param>
public sealed record DeferInboxResponse(MessageId Id, DateTimeOffset Until);
