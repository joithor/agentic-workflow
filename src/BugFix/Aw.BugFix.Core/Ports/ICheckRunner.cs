namespace Aw.BugFix.Core.Ports;

/// <summary>Runs a test command; a command that can't start, times out or dies on a signal counts as failing.</summary>
public interface ICheckRunner
{
    /// <summary>Runs <paramref name="command"/> in <paramref name="directory"/>, with stdout and stderr in <paramref name="log"/>.</summary>
    /// <param name="command">The argv.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="log">The log file.</param>
    /// <returns>The exit code; non-zero when the command didn't run to a clean exit.</returns>
    public int Run(IReadOnlyList<string> command, string directory, string log);
}
