namespace Aw.BugFix.Features.RunTest;

/// <summary>The run-test: run the regression test and register its run record.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Check">The test file.</param>
/// <param name="WorkingDirectory">The checkout to run in.</param>
/// <param name="Command">The test command (argv); it must name the check file.</param>
public sealed record RunTestRequest(
    string StateDirectory,
    string Check,
    string WorkingDirectory,
    IReadOnlyList<string> Command);
