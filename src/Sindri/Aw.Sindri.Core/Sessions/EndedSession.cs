namespace Aw.Sindri.Core.Sessions;

/// <summary>A session that left a good pass, kept for 10 minutes with its resume command (collector spec §4.1 step 8).</summary>
/// <param name="Session">The session as last seen.</param>
/// <param name="EndedAt">When the pass noticed it was gone.</param>
public sealed record EndedSession(ObservedSession Session, DateTimeOffset EndedAt);
