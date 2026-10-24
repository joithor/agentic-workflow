namespace Aw.Evidence.Core.Publishing;

/// <summary>What gh did: its exit code and what it wrote.</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Stdout">Standard output.</param>
/// <param name="Stderr">Standard error.</param>
public sealed record GhOutcome(int ExitCode, string Stdout, string Stderr);
