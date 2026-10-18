namespace Aw.Sindri.Core.Sessions;

/// <summary>What owns and shows a session (collector spec §6.1).</summary>
/// <param name="Kind">'tmux', 'warp', 'terminal', 'ide', 'ssh', 't3' or 'unknown'.</param>
/// <param name="Label">The short text a row shows ('tmux · Warp').</param>
public sealed record HostInfo(string Kind, string Label)
{
    /// <summary>A session whose host isn't detected.</summary>
    public static HostInfo Unknown { get; } = new("unknown", "unknown");

    /// <summary>The terminal that shows it: its own for 'warp' and 'terminal', the attached client's for 'tmux' (null with no client).</summary>
    public TerminalFacts? Terminal { get; init; }

    /// <summary>The tmux place, for 'tmux'.</summary>
    public TmuxFacts? Tmux { get; init; }

    /// <summary>The IDE's id, for 'ide'.</summary>
    public string? Ide { get; init; }
}
