namespace Aw.Judge.Tests;

/// <summary>What one judge command line produced: the exit code and the exact bytes of stdout and stderr (as text).</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Stdout">Stdout, including the trailing newline the CLI adds to non-empty output.</param>
/// <param name="Stderr">Stderr, likewise.</param>
public sealed record CliRun(int ExitCode, string Stdout, string Stderr);
