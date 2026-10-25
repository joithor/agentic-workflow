namespace Aw.Sindri.Core.BugFix;

/// <summary>How an attempt dispatches implementers: the two modes the recipes run (DT spec 6.1). bugfix-state's mode B (competing candidates) needs fan-out and stays out.</summary>
public enum AttemptMode
{
    /// <summary>One implementer on one worktree (bugfix-state mode A).</summary>
    Whole = 1,

    /// <summary>One implementer per area, in sequence on one worktree (bugfix-state mode C).</summary>
    Split = 2,
}
