namespace Aw.Sindri.Core.Sessions;

/// <summary>What owns and shows a session, as a client sees it (collector spec CA-1, CA-2).</summary>
/// <param name="Kind">'tmux', 'warp', 'terminal', 'ide', 'ssh', 't3' or 'unknown'.</param>
/// <param name="Label">The short text a row shows ('tmux · Warp').</param>
/// <param name="SocketKind">'sindri', 'team', 'default' or 'other' for tmux; null otherwise.</param>
/// <param name="AttachedClients">How many tmux clients show the session, or null when unknown.</param>
/// <param name="Terminal">The terminal that shows it ('warp', 'kitty', …), or null.</param>
public sealed record SessionHostRow(string Kind, string Label, string? SocketKind, int? AttachedClients, string? Terminal)
{
    /// <summary>The tmux session name (human callers only).</summary>
    public string? Session { get; init; }

    /// <summary>The tmux pane id (human callers only).</summary>
    public string? Pane { get; init; }

    /// <summary>The tmux socket path (human callers of the single-session read only).</summary>
    public string? Socket { get; init; }
}
