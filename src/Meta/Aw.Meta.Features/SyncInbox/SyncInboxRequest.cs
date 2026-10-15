using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.SyncInbox;

/// <summary>The SyncInbox request: read a session's transcript for peer messages not seen yet.</summary>
/// <param name="Session">The session whose transcript it is.</param>
/// <param name="Transcript">The transcript file path.</param>
public sealed record SyncInboxRequest(SessionId Session, string Transcript);
