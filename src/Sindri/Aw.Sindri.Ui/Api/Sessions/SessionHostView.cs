namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>Where a session runs.</summary>
/// <param name="Kind">Kind: tmux, warp, kitty and the like, or unknown.</param>
/// <param name="Label">Label, as served ("tmux · Warp").</param>
/// <param name="SocketKind">SocketKind, as served.</param>
/// <param name="AttachedClients">AttachedClients, as served.</param>
/// <param name="Terminal">Terminal, as served.</param>
/// <param name="Session">Session: the tmux session's name; null for a session caller.</param>
/// <param name="Pane">Pane, as served; null for a session caller.</param>
/// <param name="Socket">Socket: the tmux socket path; served on one session only.</param>
internal sealed record SessionHostView(
    string Kind,
    string Label,
    string? SocketKind,
    int? AttachedClients,
    string? Terminal,
    string? Session,
    string? Pane,
    string? Socket);
