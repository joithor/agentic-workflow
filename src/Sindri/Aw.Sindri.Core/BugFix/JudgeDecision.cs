namespace Aw.Sindri.Core.BugFix;

/// <summary>judge resolution-check's decision on a candidate.</summary>
public enum JudgeDecision
{
    /// <summary>The reported problem is solved.</summary>
    Resolved = 1,

    /// <summary>Part of it is solved.</summary>
    Partial = 2,

    /// <summary>It is not solved.</summary>
    Unresolved = 3,

    /// <summary>judge could not decide and asks a human.</summary>
    Escalated = 4,
}
