namespace Aw.Evidence.Testing;

/// <summary>What a program should do for a <see cref="CliRunnerContract"/> case.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="Stdout">What it writes to standard output.</param>
public sealed record CliProgram(int ExitCode, string Stdout);
