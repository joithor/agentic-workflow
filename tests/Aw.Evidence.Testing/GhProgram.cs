namespace Aw.Evidence.Testing;

/// <summary>What gh should do for a <see cref="GhRunnerContract"/> case.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="Stdout">What it writes to standard output.</param>
/// <param name="Stderr">What it writes to standard error.</param>
public sealed record GhProgram(int ExitCode, string Stdout, string Stderr);
