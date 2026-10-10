namespace Aw.Readiness.Core;

/// <summary>What one run leaves in the history: enough for the next run's delta, and a versioned record a later
/// ledger can import (spec §8, §4c).</summary>
/// <param name="SchemaVersion">The record format (<see cref="CurrentSchemaVersion"/>).</param>
/// <param name="Repo">The repo.</param>
/// <param name="Scope">What was checked (readiness.yaml 'scope'); runs are compared only within a scope.</param>
/// <param name="RecordedAt">When the run happened (UTC).</param>
/// <param name="Level">The overall level it reached.</param>
/// <param name="States">Each criterion's state.</param>
public sealed record RunRecord(
    int SchemaVersion, RepoName Repo, string Scope, DateTimeOffset RecordedAt, int Level, IReadOnlyList<RecordedState> States)
{
    /// <summary>The record format this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The run's record from its findings.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="scope">What was checked.</param>
    /// <param name="recordedAt">When it ran.</param>
    /// <param name="level">The overall level.</param>
    /// <param name="findings">The findings.</param>
    /// <returns>The record.</returns>
    public static RunRecord From(RepoName repo, string scope, DateTimeOffset recordedAt, int level, IEnumerable<Finding> findings) =>
        new(CurrentSchemaVersion, repo, scope, recordedAt, level,
            [.. findings.Select(f => new RecordedState(f.Criterion.Id, f.State, f.Unread))]);
}
