namespace Aw.BugFix.Features.AdvanceReproduce;

/// <summary>The advance reproduce: freeze the check and the failing baseline.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Evidence">The baseline run record (from run-test or run-ui).</param>
/// <param name="Check">The check file.</param>
/// <param name="WorkingDirectory">The checkout the check ran in.</param>
public sealed record AdvanceReproduceRequest(
    string StateDirectory,
    string Evidence,
    string Check,
    string WorkingDirectory);
