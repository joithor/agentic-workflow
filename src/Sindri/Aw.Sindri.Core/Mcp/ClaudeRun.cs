namespace Aw.Sindri.Core.Mcp;

/// <summary>How a claude run ended.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="ErrorTail">The last line of its standard error, or empty.</param>
public sealed record ClaudeRun(int ExitCode, string ErrorTail);
