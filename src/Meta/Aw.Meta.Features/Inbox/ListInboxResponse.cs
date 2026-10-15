namespace Aw.Meta.Features.Inbox;

/// <summary>The ListInbox response.</summary>
/// <param name="Items">The messages, oldest first.</param>
public sealed record ListInboxResponse(IReadOnlyList<InboxItem> Items);
