namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>One ancestor in a probe report: its name as invoked (argv[0]'s basename) and its executable's path, the home directory written '~'.</summary>
/// <param name="Name">The name, e.g. 'claude', '-zsh' or 'launchd'.</param>
/// <param name="Executable">The executable's path ('~/.local/share/claude/versions/2.1.296'), or null when the OS won't show it.</param>
internal sealed record ProbedProcess(string Name, string? Executable);
