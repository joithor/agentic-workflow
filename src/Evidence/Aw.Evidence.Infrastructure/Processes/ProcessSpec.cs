namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>A program to run: the command, its input and, when given, the exact environment and the working directory.</summary>
/// <param name="Command">The program, then its arguments.</param>
/// <param name="StandardInput">Text to write to its standard input, or null to leave it unredirected.</param>
/// <param name="Environment">The whole environment (nothing is inherited), or null to inherit.</param>
/// <param name="WorkingDirectory">Where it runs, or null for the current directory.</param>
internal sealed record ProcessSpec(
    IReadOnlyList<string> Command, string? StandardInput, IReadOnlyDictionary<string, string>? Environment, string? WorkingDirectory);
