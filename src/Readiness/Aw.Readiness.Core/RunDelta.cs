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

    /// <summary>The delta from a previous run (or none) to this one.</summary>
    /// <param name="previous">The previous run, or null.</param>
    /// <param name="current">This run.</param>
    /// <returns>The delta.</returns>
    public static RunDelta Between(RunRecord? previous, RunRecord current)
        => throw new NotSupportedException("red: RunDelta.Between");
}
