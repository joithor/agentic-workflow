namespace Aw.Sindri.Core.Control;

/// <summary>One ancestor of a process: its pid and its name (the executable's short name, as the OS reports it).</summary>
/// <param name="Pid">Its process id.</param>
/// <param name="Name">Its name, e.g. 'zsh', 'claude' or 'launchd'.</param>
public sealed record ProcessAncestor(int Pid, string Name);
