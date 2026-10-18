using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.ListSessions;

/// <summary>A session that ended in the last ten minutes (collector spec CA-1).</summary>
/// <param name="Session">The session as last seen.</param>
/// <param name="EndedAt">When the collector noticed it was gone.</param>
/// <param name="Resume">The argv that resumes it (human callers only; null when the agent has no resume form or the id is unknown).</param>
public sealed record EndedSessionRow(SessionRow Session, DateTimeOffset EndedAt, IReadOnlyList<string>? Resume);
