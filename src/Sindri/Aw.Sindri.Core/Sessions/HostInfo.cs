namespace Aw.Sindri.Core.Sessions;

/// <summary>What owns and shows a session (collector spec §6.1). CO-2a knows only 'unknown'; host detection is CO-2b's.</summary>
/// <param name="Kind">'tmux', 'warp', 'terminal', 'ide', 'ssh', 't3' or 'unknown'.</param>
/// <param name="Label">The short text a row shows.</param>
public sealed record HostInfo(string Kind, string Label)
{
    /// <summary>A session whose host isn't detected.</summary>
    public static HostInfo Unknown { get; } = new("unknown", "unknown");
}
