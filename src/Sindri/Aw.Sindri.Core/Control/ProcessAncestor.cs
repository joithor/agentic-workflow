namespace Aw.Sindri.Core.Control;

/// <summary>One ancestor of a process: its pid and its name (the executable's short name, as the OS reports it).</summary>
/// <param name="Pid">Its process id.</param>
/// <param name="Name">Its name, e.g. 'zsh', 'claude' or 'launchd'.</param>
public sealed record ProcessAncestor(int Pid, string Name)
{
    /// <summary>
    /// The full path of the executable the process runs, as the OS reports it ('~/.local/share/claude/versions/2.1.296'
    /// for Claude Code invoked as 'claude'), or null when it can't be read (engine spec §6.2: the probe records it).
    /// </summary>
    public string? Executable { get; init; }
}
