namespace Aw.BugFix.Core.Model;

/// <summary>The evidence file run-test and run-ui write: one run of the check, classified.</summary>
/// <param name="Kind">Test or ui-evidence.</param>
/// <param name="Outcome">Passed, failed or broken (broken: ui steps broke and none failed).</param>
/// <param name="Commit">The commit the run executed against.</param>
/// <param name="CheckSha256">SHA-256 of the check file the run executed.</param>
/// <param name="Command">The test command (argv); null for ui-evidence.</param>
/// <param name="ExitCode">The test command's or ui-evidence's exit code.</param>
/// <param name="Output">The run's log (test) or ui-evidence run directory.</param>
public sealed record RunRecord(
    CheckKind Kind,
    RunOutcome Outcome,
    string Commit,
    string CheckSha256,
    IReadOnlyList<string>? Command,
    int ExitCode,
    string Output);
