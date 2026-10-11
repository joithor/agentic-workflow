using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Infrastructure;

/// <summary>Runs a test command with its output streamed to the log; a command that can't start or hits the time limit didn't run to a verdict.</summary>
internal sealed class ProcessCheckRunner(BugFixSettings settings) : ICheckRunner
{
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log)
    {
        var (code, ran) = Processes.ToLog(Processes.Start(command[0], command.Skip(1), directory), settings.CheckTimeout, log);
        return new CheckExit(code, ran);
    }
}
