using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.InboxDefer;

/// <summary>The DeferInbox request: snooze a reply obligation.</summary>
/// <param name="Id">The message.</param>
/// <param name="Reason">Why it waits.</param>
/// <param name="Until">When it reopens: "+30m", "+2h", "+1d", or an ISO 8601 time.</param>
public sealed record DeferInboxRequest(MessageId Id, string Reason, string Until);
