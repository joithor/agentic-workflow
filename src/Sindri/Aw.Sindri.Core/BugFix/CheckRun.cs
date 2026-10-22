namespace Aw.Sindri.Core.BugFix;

/// <summary>The frozen check run against a candidate and recorded for it.</summary>
/// <param name="Candidate">The candidate.</param>
/// <param name="Passed">Whether the check passed.</param>
/// <param name="ExitCode">The check command's exit code; 0 for a ui check that passed.</param>
/// <param name="Evidence">The run record's path.</param>
public sealed record CheckRun(string Candidate, bool Passed, int ExitCode, string Evidence);
