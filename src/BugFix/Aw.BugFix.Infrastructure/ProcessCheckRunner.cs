using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Infrastructure;

/// <summary>Runs a test command with its output in the log; a command that hangs is killed at the time limit and counts as failing.</summary>
internal sealed class ProcessCheckRunner(BugFixSettings settings) : ICheckRunner
{
    public int Run(IReadOnlyList<string> command, string directory, string log)
    {
        var start = Processes.Start(command[0], command.Skip(1), directory);
        var run = Processes.Capture(start, settings.CheckTimeout);
        File.WriteAllText(log, run.Output + run.Error);
        return run.ExitCode;
    }
}
