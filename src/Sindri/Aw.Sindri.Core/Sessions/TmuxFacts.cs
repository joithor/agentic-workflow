namespace Aw.Sindri.Core.Sessions;

/// <summary>Where a tmux-hosted session lives (collector spec §6.1). The socket is for human callers only.</summary>
/// <param name="Socket">The server's socket path, or null when the server couldn't be reached.</param>
/// <param name="SocketKind">'sindri', 'team', 'default' or 'other'.</param>
/// <param name="Session">The tmux session name, or null when unknown.</param>
/// <param name="Pane">The pane id ('%3'), or null when unknown.</param>
public sealed record TmuxFacts(string? Socket, string SocketKind, string? Session, string? Pane);
