using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// Runs a test command with its output streamed to the log and without the loader variables a caller could inject
/// (BASH_ENV, NODE_OPTIONS, …), and reports which program it ran: a command that can't start or hits the time limit
/// didn't run to a verdict.
/// </summary>
internal sealed class ProcessCheckRunner(BugFixSettings settings, IFiles files) : ICheckRunner
{
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log)
    {
        var program = Program(command[0], directory);
        var start = Processes.Start(command[0], command.Skip(1), directory);
        CheckEnvironment.StripLoaders(start.Environment);
        var (code, ran) = Processes.ToLog(start, settings.CheckTimeout, log);
        return new CheckExit(code, ran, program is null ? string.Empty : files.Sha256(program).Match(static s => s, static _ => string.Empty));
    }

    // The file argv[0] runs, as the OS finds it: a path relative to the directory, or the first match on PATH.
    private string? Program(string name, string directory)
    {
        if (name.Contains('/', StringComparison.Ordinal))
        {
            return files.RealPath(files.FullPath(directory, name));
        }

        return (settings.Path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => files.RealPath(files.FullPath(Path.IsPathRooted(dir) ? dir : Path.Join(directory, dir), name)))
            .FirstOrDefault(static p => p is not null);
    }
}
