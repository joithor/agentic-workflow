namespace Aw.Readiness.Core.Scoring;

/// <summary>How many of one level's scored criteria passed. Only pass and fail are scored.</summary>
/// <param name="Level">The level (1-5).</param>
/// <param name="Passed">Scored criteria that passed.</param>
/// <param name="Scored">Criteria scored (pass or fail) at this level.</param>
public sealed record LevelTally(int Level, int Passed, int Scored)
{
    /// <summary>The share of scored criteria a level needs to pass before the next one unlocks (spec §2).</summary>
    public const int UnlockPercent = 80;

    /// <summary>True when nothing was scored at this level: it neither unlocks nor blocks.</summary>
    public bool IsEmpty => Scored == 0;

    /// <summary>True when at least <see cref="UnlockPercent"/>% of the scored criteria passed.</summary>
    public bool Unlocks => !IsEmpty && Passed * 100 >= Scored * UnlockPercent;
}
