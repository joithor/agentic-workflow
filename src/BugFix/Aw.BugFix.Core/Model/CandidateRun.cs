namespace Aw.BugFix.Core.Model;

/// <summary>The frozen check's run against a candidate.</summary>
/// <param name="Evidence">The run record's full path.</param>
/// <param name="Passed">Whether the check passed.</param>
/// <param name="RecordedAt">When record-run recorded it.</param>
public sealed record CandidateRun(string Evidence, bool Passed, DateTimeOffset RecordedAt);
