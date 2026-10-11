namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>How a finished child process ended.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="StandardOutput">Everything it wrote to standard output.</param>
/// <param name="StandardError">Everything it wrote to standard error.</param>
internal sealed record ProcessOutcome(int ExitCode, string StandardOutput, string StandardError);
