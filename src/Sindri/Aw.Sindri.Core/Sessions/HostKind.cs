namespace Aw.Sindri.Core.Sessions;

/// <summary>What kind of process a host-detection walk stopped at (collector spec §6.1's table).</summary>
public enum HostKind
{
    /// <summary>A 'tmux: server' process.</summary>
    Tmux,

    /// <summary>sshd.</summary>
    Ssh,

    /// <summary>A terminals.json terminal.</summary>
    Terminal,

    /// <summary>Warp.</summary>
    Warp,

    /// <summary>An IDE (code, cursor).</summary>
    Ide,
}
