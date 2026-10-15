namespace Aw.Meta.Features.Inbox;

/// <summary>The ListInbox request.</summary>
/// <param name="All">Whether to list every message; otherwise only those still owed a reply.</param>
public sealed record ListInboxRequest(bool All);
