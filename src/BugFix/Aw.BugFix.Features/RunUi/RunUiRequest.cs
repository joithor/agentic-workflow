namespace Aw.BugFix.Features.RunUi;

/// <summary>The run-ui: run the ui-evidence script and register its run record.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Check">The ui-evidence script.</param>
/// <param name="WorkingDirectory">The checkout the app was built from.</param>
public sealed record RunUiRequest(
    string StateDirectory,
    string Check,
    string WorkingDirectory);
