namespace Aw.Meta.Core.Gate;

/// <summary>The outcome of a gate run or one of its steps.</summary>
public enum GateVerdict
{
    /// <summary>Everything passed.</summary>
    Pass = 1,

    /// <summary>Something failed.</summary>
    Fail = 2,

    /// <summary>The pull request does not merge cleanly onto main.</summary>
    Conflict = 3,
}
