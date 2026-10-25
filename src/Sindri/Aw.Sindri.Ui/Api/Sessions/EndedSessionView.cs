namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>A session that ended recently, with the command that resumes it.</summary>
/// <param name="Session">Session: the row as it last was.</param>
/// <param name="EndedAt">EndedAt, as served.</param>
/// <param name="Resume">Resume: the argv, or null when none is known (or hidden from a session caller).</param>
internal sealed record EndedSessionView(SessionRowView Session, DateTimeOffset EndedAt, IReadOnlyList<string>? Resume);
