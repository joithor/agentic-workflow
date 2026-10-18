namespace Aw.Meta.Core.Lanes;

/// <summary>What a lane's context size says to do.</summary>
public enum ContextVerdict
{
    /// <summary>Under the threshold.</summary>
    Ok = 1,

    /// <summary>At or above the threshold: the lane should write a digest and be recycled.</summary>
    Recycle = 2,

    /// <summary>The size can't be told: no transcript or usage, or a lane that isn't Claude Code.</summary>
    Unknown = 3,
}
