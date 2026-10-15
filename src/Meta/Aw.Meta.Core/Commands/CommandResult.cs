namespace Aw.Meta.Core.Commands;

/// <summary>What a command did.</summary>
/// <param name="ExitCode">The exit code; -1 when it was killed on a timeout.</param>
/// <param name="Stdout">The tail of standard output.</param>
/// <param name="Stderr">The tail of standard error.</param>
/// <param name="TimedOut">Whether it was killed on the timeout.</param>
public sealed record CommandResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);
