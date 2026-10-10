using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Testing;

/// <summary>The fake for <see cref="ICheckRunner"/>: each program exits with the code it was given (true 0, false 1); an unknown program can't start (127).</summary>
public sealed class ScriptedCheckRunner : ICheckRunner
{
    private readonly Dictionary<string, int> _programs = new(StringComparer.Ordinal) { ["true"] = 0, ["false"] = 1 };

    /// <summary>Gets every command run, with its directory.</summary>
    public IList<(IReadOnlyList<string> Command, string Directory)> Runs { get; } = [];

    /// <summary>Makes <paramref name="program"/> exit with <paramref name="exitCode"/>.</summary>
    /// <param name="program">The program (argv[0]).</param>
    /// <param name="exitCode">Its exit code.</param>
    /// <returns>This runner.</returns>
    public ScriptedCheckRunner Exits(string program, int exitCode)
    {
        _programs[program] = exitCode;
        return this;
    }

    /// <inheritdoc/>
    public int Run(IReadOnlyList<string> command, string directory, string log)
    {
        Runs.Add((command, directory));
        return _programs.GetValueOrDefault(command[0], 127);
    }
}
