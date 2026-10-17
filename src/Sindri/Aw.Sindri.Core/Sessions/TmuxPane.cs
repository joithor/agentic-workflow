namespace Aw.Sindri.Core.Sessions;

/// <summary>One row of 'tmux list-panes -a'.</summary>
/// <param name="PanePid">The pane's first process.</param>
/// <param name="Session">The session name.</param>
/// <param name="Window">The window index.</param>
/// <param name="Index">The pane index.</param>
/// <param name="PaneId">The pane id, '%n'.</param>
public sealed record TmuxPane(int PanePid, string Session, int Window, int Index, string PaneId);
