namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>GET /sessions/{provider}/{id}: one session with what only its sheet shows.</summary>
/// <param name="Session">Session, as served (with the host's socket).</param>
/// <param name="Cwd">Cwd, as served.</param>
/// <param name="StateHistory">StateHistory, as served (oldest first); null for a session caller.</param>
/// <param name="AsOf">AsOf, as served.</param>
internal sealed record SessionView(SessionRowView Session, string? Cwd, IReadOnlyList<StateHistoryEntryView>? StateHistory, DateTimeOffset AsOf);
