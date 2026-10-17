namespace Aw.Meta.Core.Gate;

/// <summary>How a pull request fared in a train.</summary>
public enum TrainState
{
    /// <summary>Its gate is running.</summary>
    Gating,

    /// <summary>Its gate passed.</summary>
    Pass,

    /// <summary>Its gate failed.</summary>
    Fail,

    /// <summary>Its head conflicts with main or with a pull request ahead of it; it is left out of this round.</summary>
    Dropped,

    /// <summary>Its gate was stopped because a gate below it failed; nothing was recorded and it stays queued.</summary>
    Cancelled,

    /// <summary>Its gate could not run (a git, gh or process error).</summary>
    Error,

    /// <summary>It is a draft; left out of this round.</summary>
    Skipped,
}
