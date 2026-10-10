namespace Aw.BugFix.Infrastructure;

/// <summary>A finished process: its exit code and output.</summary>
/// <param name="ExitCode">The exit code (127 when it couldn't start, 124 when killed at its time limit).</param>
/// <param name="Output">Standard output.</param>
/// <param name="Error">Standard error.</param>
internal sealed record ProcessResult(int ExitCode, string Output, string Error);
