namespace Aw.Sindri.Core.Sessions;

/// <summary>What shows a session, read from the shell under the terminal (collector spec §6.1): the ids focus needs later. Human callers only.</summary>
/// <param name="Name">'warp', 'ssh', a terminals.json id ('kitty', 'wezterm', …) or an IDE id.</param>
/// <param name="Pid">The terminal process's id.</param>
public sealed record TerminalFacts(string Name, int Pid)
{
    /// <summary>The X11 window id (WINDOWID).</summary>
    public string? XWindow { get; init; }

    /// <summary>kitty's window id.</summary>
    public string? KittyWindow { get; init; }

    /// <summary>kitty's remote-control address.</summary>
    public string? KittyListenOn { get; init; }

    /// <summary>wezterm's pane id.</summary>
    public string? WeztermPane { get; init; }

    /// <summary>Warp's tab URL, only when it has the form warp://session/&lt;32 hex&gt;.</summary>
    public string? WarpFocusUrl { get; init; }
}
