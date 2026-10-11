using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Core.Ports;

/// <summary>Runs a test command with its output in a log, within a time limit.</summary>
public interface ICheckRunner
{
    /// <summary>Runs <paramref name="command"/> in <paramref name="directory"/>, with stdout and stderr in <paramref name="log"/>.</summary>
    /// <param name="command">The argv.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="log">The log file.</param>
    /// <returns>How it ended; not <see cref="CheckExit.Ran"/> when it couldn't start or was killed at the time limit.</returns>
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log);
}
