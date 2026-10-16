namespace Aw.Meta.Core.Worktrees;

/// <summary>Whether a sweep removes what the rules allow, or only reports it.</summary>
public enum SweepMode
{
    /// <summary>Remove the removable worktrees.</summary>
    Remove = 1,

    /// <summary>Report what would be removed; touch nothing.</summary>
    DryRun = 2,
}
