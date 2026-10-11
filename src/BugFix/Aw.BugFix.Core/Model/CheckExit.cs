namespace Aw.BugFix.Core.Model;

/// <summary>How a test command ended, and which program it ran.</summary>
/// <param name="Code">Its exit code (meaningful only when <paramref name="Ran"/>).</param>
/// <param name="Ran">False when the command couldn't start or was killed at the time limit: the check didn't run to a verdict.</param>
/// <param name="ProgramSha256">The SHA-256 of the program argv[0] resolved to (after the PATH lookup); empty when it didn't resolve.</param>
public sealed record CheckExit(int Code, bool Ran, string ProgramSha256)
{
    /// <summary>Gets the run's outcome: broken when it didn't run to a verdict, passed on exit 0, failed otherwise.</summary>
    public RunOutcome Outcome => !Ran ? RunOutcome.Broken : Code == 0 ? RunOutcome.Passed : RunOutcome.Failed;
}
