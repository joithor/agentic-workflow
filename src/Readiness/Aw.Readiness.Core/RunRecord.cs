namespace Aw.Readiness.Core;

/// <summary>What one run leaves in the history: enough for the next run's delta.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="RecordedAt">When the run happened (UTC).</param>
/// <param name="Level">The overall level it reached.</param>
/// <param name="States">Each criterion's state.</param>
public sealed record RunRecord(RepoName Repo, DateTimeOffset RecordedAt, int Level, IReadOnlyList<RecordedState> States)
{
    /// <summary>The run's record from its findings.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="recordedAt">When it ran.</param>
    /// <param name="level">The overall level.</param>
    /// <param name="findings">The findings.</param>
    /// <returns>The record.</returns>
    public static RunRecord From(RepoName repo, DateTimeOffset recordedAt, int level, IEnumerable<Finding> findings) =>
        new(repo, recordedAt, level, [.. findings.Select(f => new RecordedState(f.Criterion.Id, f.State))]);
}
