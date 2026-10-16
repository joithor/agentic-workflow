namespace Aw.Meta.Core.Worktrees;

/// <summary>What the rules say about one worktree.</summary>
public enum WorktreeDecision
{
    /// <summary>Every rule holds: nothing in it can be lost, and nothing is using it.</summary>
    Removable = 1,

    /// <summary>At least one rule fails; the reasons say which.</summary>
    Kept = 2,
}
