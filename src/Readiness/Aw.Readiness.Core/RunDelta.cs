namespace Aw.Readiness.Core;

/// <summary>What changed since the previous run (spec §8). No previous run means this one is the baseline.</summary>
/// <param name="Since">When the previous run happened, or null on a first run.</param>
/// <param name="LevelBefore">The previous run's level, or null on a first run.</param>
/// <param name="LevelAfter">This run's level.</param>
/// <param name="Changes">Criteria whose state changed, in id order.</param>
public sealed record RunDelta(DateTimeOffset? Since, int? LevelBefore, int LevelAfter, IReadOnlyList<StateChange> Changes)
{
    /// <summary>True when there was no previous run, so this run is the baseline.</summary>
    public bool IsFirstRun => Since is null;

    /// <summary>The delta from a previous run (or none) to this one. A criterion unread on either run isn't compared,
    /// so an offline run doesn't show host criteria flipping to and from unknown.</summary>
    /// <param name="previous">The previous run, or null.</param>
    /// <param name="current">This run.</param>
    /// <returns>The delta.</returns>
    public static RunDelta Between(RunRecord? previous, RunRecord current)
    {
        if (previous is null)
        {
            return new RunDelta(Since: null, LevelBefore: null, current.Level, []);
        }

        var unread = previous.States.Concat(current.States).Where(s => s.Unread).Select(s => s.Id.Value).ToHashSet(StringComparer.Ordinal);
        var before = previous.States.ToDictionary(s => s.Id.Value, s => s.State, StringComparer.Ordinal);
        var after = current.States.ToDictionary(s => s.Id.Value, s => s.State, StringComparer.Ordinal);
        var changes = before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(id => new StateChange(CriterionId.From(id), StateOf(before, id), StateOf(after, id)))
            .Where(c => c.Before != c.After)
            .ToList();
        return new RunDelta(previous.RecordedAt, previous.Level, current.Level, changes);
    }

    private static CriterionState? StateOf(Dictionary<string, CriterionState> states, string id) =>
        states.TryGetValue(id, out var state) ? state : null;
}
