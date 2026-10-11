namespace Aw.BugFix.Core.Model;

/// <summary>What a ui-evidence run reports: each step's outcome and the hash of the script it executed.</summary>
/// <param name="Steps">Each step's outcome, in order.</param>
/// <param name="ScriptSha256">SHA-256 of the executed script.</param>
/// <param name="ExitCode">ui-evidence's exit code.</param>
public sealed record UiRun(IReadOnlyList<RunOutcome> Steps, string ScriptSha256, int ExitCode)
{
    /// <summary>Gets the run's outcome: failed if any step failed (a real behaviour failure), broken if steps broke and none failed (a selector problem), else passed.</summary>
    public RunOutcome Outcome =>
        Steps.Contains(RunOutcome.Failed) ? RunOutcome.Failed
        : Steps.Contains(RunOutcome.Broken) || Steps.Count == 0 ? RunOutcome.Broken
        : RunOutcome.Passed;
}
