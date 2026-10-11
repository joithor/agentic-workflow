using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// Runs a test command as the file argv[0] resolves to (resolved once, hashed, and started as that file), with only the
/// allowed environment variables, its output streamed to the log. A command that can't start or hits the time limit
/// didn't run to a verdict.
/// </summary>
internal sealed class ProcessCheckRunner(BugFixSettings settings, IFiles files) : ICheckRunner
{
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log)
    {
        var environmentSha = CheckEnvironment.Hash(settings.CheckVariables);
        if (Program(command[0], directory) is not { } program)
        {
            File.WriteAllText(log, $"bugfix-state: {command[0]} isn't a file in {directory} or on PATH; it can't start\n");
            return new CheckExit(Processes.CouldNotStart, Ran: false, string.Empty, environmentSha);
        }

        var start = Processes.Start(program, command.Skip(1), directory);
        CheckEnvironment.Only(start.Environment, settings.CheckVariables);
        var (code, ran) = Processes.ToLog(start, settings.CheckTimeout, log);
        return new CheckExit(code, ran, files.Sha256(program).Match(static s => s, static _ => string.Empty), environmentSha);
    }

    // The file argv[0] names, as the OS would find it from the run's directory: a path relative to the directory, or the
    // first match on the check's PATH.
    private string? Program(string name, string directory)
    {
        if (name.Contains('/', StringComparison.Ordinal))
        {
            return files.RealPath(files.FullPath(directory, name));
        }

        return settings.CheckVariables.GetValueOrDefault("PATH", string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => files.RealPath(files.FullPath(Path.IsPathRooted(dir) ? dir : Path.Join(directory, dir), name)))
            .FirstOrDefault(static p => p is not null);
    }
}
