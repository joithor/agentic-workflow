using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Testing;

/// <summary>The fake for <see cref="ICheckRunner"/>: each program exits with the code it was given (true 0, false 1); an unknown program can't start, and a hanging one hits the time limit.</summary>
public sealed class ScriptedCheckRunner : ICheckRunner
{
    private readonly Dictionary<string, int> _programs = new(StringComparer.Ordinal) { ["true"] = 0, ["false"] = 1 };
    private readonly HashSet<string> _hanging = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _resolves = new(StringComparer.Ordinal);
    private Dictionary<string, string> _environment = new(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin" };

    /// <summary>Gets or sets what the command does to its directory while it runs (writes a report, say).</summary>
    public Action<string>? WhileRunning { get; set; }

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

    /// <summary>Makes <paramref name="program"/> resolve to a file with SHA-256 <paramref name="sha"/> (as a changed PATH would).</summary>
    /// <param name="program">The program (argv[0]).</param>
    /// <param name="sha">The resolved file's hash.</param>
    /// <returns>This runner.</returns>
    public ScriptedCheckRunner Resolves(string program, string sha)
    {
        _resolves[program] = sha;
        return this;
    }

    /// <summary>Makes later runs report another PATH, as a caller's changed shell would.</summary>
    /// <param name="path">The PATH value.</param>
    /// <returns>This runner.</returns>
    public ScriptedCheckRunner RunsWithPath(string path)
    {
        _environment = new(StringComparer.Ordinal) { ["PATH"] = path };
        return this;
    }

    /// <inheritdoc/>
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log, string digestKey)
    {
        Runs.Add((command, directory));
        WhileRunning?.Invoke(directory);
        var program = _resolves.GetValueOrDefault(command[0], $"sha-of-{command[0]}");
        if (_hanging.Contains(command[0]))
        {
            return new CheckExit(124, Ran: false, program, _environment);
        }

        return _programs.TryGetValue(command[0], out var code) ? new CheckExit(code, Ran: true, program, _environment) : new CheckExit(127, Ran: false, string.Empty, _environment);
    }
}
