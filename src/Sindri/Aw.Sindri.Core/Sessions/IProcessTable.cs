using Aw.Kernel;

namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// The process table (collector spec §4.1 steps 1, 2, 5, 6): the current user's live processes, a process's allowlisted environment, its
/// working directory and its open files. It reads argv[0] only up to the first NUL; the rest of a command line (which may be a prompt) is never read.
/// </summary>
public interface IProcessTable
{
    /// <summary>The current user's processes, zombies and other users' processes left out.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The table, or an error when the process table can't be read at all.</returns>
    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken);

    /// <summary>The environment of <paramref name="pid"/> reduced to <see cref="EnvironmentAllowlist"/>; every other name is dropped while parsing.</summary>
    /// <param name="pid">The process.</param>
    /// <returns>The allowlisted names and values; empty when unreadable.</returns>
    public IReadOnlyDictionary<string, string> Environment(int pid);

    /// <summary>The process's working directory.</summary>
    /// <param name="pid">The process.</param>
    /// <returns>The path, or null when unreadable.</returns>
    public string? Cwd(int pid);

    /// <summary>The targets of the process's open file links, for a transcript among them.</summary>
    /// <param name="pid">The process.</param>
    /// <returns>The paths; empty when unreadable.</returns>
    public IReadOnlyList<string> OpenFiles(int pid);
}
