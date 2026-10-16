namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>The end of a tool run.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="ExitCode">The exit code when it exited, otherwise -1.</param>
/// <param name="Output">Its standard output when it exited, otherwise empty. May hold a secret: never log it.</param>
internal sealed record ToolRun(ToolOutcome Outcome, int ExitCode, string Output)
{
    /// <summary>The tool wasn't on PATH.</summary>
    internal static ToolRun Missing { get; } = new(ToolOutcome.Missing, -1, "");

    /// <summary>The tool ran past its timeout.</summary>
    internal static ToolRun TimedOut { get; } = new(ToolOutcome.TimedOut, -1, "");
}
