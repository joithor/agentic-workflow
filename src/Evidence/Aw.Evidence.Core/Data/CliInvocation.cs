namespace Aw.Evidence.Core.Data;

/// <summary>One program to run: an absolute program path, its arguments, standard input and the exact environment it gets.</summary>
/// <param name="Executable">The program (absolute path, symlinks already resolved); never looked up on PATH.</param>
/// <param name="Args">The arguments after the program.</param>
/// <param name="Stdin">Text written to its standard input, or null for none.</param>
/// <param name="Environment">The whole environment (nothing else is inherited); the runner adds HOME and TMPDIR.</param>
public sealed record CliInvocation(string Executable, IReadOnlyList<string> Args, string? Stdin, IReadOnlyDictionary<string, string> Environment);
