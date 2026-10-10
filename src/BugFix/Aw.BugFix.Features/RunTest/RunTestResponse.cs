using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.RunTest;

/// <summary>The result of run-test.</summary>
/// <param name="Evidence">The run record's full path.</param>
/// <param name="Outcome">Passed or failed.</param>
/// <param name="ExitCode">The test command's exit code.</param>
/// <param name="Commit">The commit it ran against.</param>
public sealed record RunTestResponse(
    string Evidence,
    RunOutcome Outcome,
    int ExitCode,
    string Commit);
