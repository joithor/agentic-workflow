using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.RunUi;

/// <summary>The result of run-ui.</summary>
/// <param name="Evidence">The run record's full path.</param>
/// <param name="Outcome">Passed, failed or broken.</param>
/// <param name="ExitCode">ui-evidence's exit code.</param>
/// <param name="Commit">The commit it ran against.</param>
public sealed record RunUiResponse(
    string Evidence,
    RunOutcome Outcome,
    int ExitCode,
    string Commit);
