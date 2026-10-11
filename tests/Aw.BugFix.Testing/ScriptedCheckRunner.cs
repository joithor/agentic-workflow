using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Testing;

/// <summary>The fake for <see cref="ICheckRunner"/>: each program exits with the code it was given (true 0, false 1); an unknown program can't start, and a hanging one hits the time limit.</summary>
public sealed class ScriptedCheckRunner : ICheckRunner
{
    private readonly Dictionary<string, int> _programs = new(StringComparer.Ordinal) { ["true"] = 0, ["false"] = 1 };
    private readonly HashSet<string> _hanging = new(StringComparer.Ordinal);

    /// <summary>Gets every command run, with its directory.</summary>
    public IList<(IReadOnlyList<string> Command, string Directory)> Runs { get; } = [];

    /// <summary>Makes <paramref name="program"/> exit with <paramref name="exitCode"/>.</summary>
    /// <param name="program">The program (argv[0]).</param>
    /// <param name="exitCode">Its exit code.</param>
    /// <returns>This runner.</returns>
    public ScriptedCheckRunner Exits(string program, int exitCode)
    {
        _programs[program] = exitCode;
        _ = _hanging.Remove(program);
        return this;
    }

    /// <summary>Makes <paramref name="program"/> hang until the time limit kills it.</summary>
    /// <param name="program">The program (argv[0]).</param>
    /// <returns>This runner.</returns>
    public ScriptedCheckRunner Hangs(string program)
    {
        _ = _hanging.Add(program);
        return this;
    }

    /// <inheritdoc/>
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log)
    {
        Runs.Add((command, directory));
        if (_hanging.Contains(command[0]))
        {
            return new CheckExit(124, Ran: false);
        }

        return _programs.TryGetValue(command[0], out var code) ? new CheckExit(code, Ran: true) : new CheckExit(127, Ran: false);
    }
}
