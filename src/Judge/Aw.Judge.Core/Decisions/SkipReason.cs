namespace Aw.Judge.Core.Decisions;

/// <summary>Why a provider in the chain didn't decide: stored as 'unavailable', 'failed', 'timeout' or 'below_threshold'.</summary>
public enum SkipReason
{
    /// <summary>The provider isn't installed or has no key.</summary>
    Unavailable = 1,

    /// <summary>The provider returned an error.</summary>
    Failed = 2,

    /// <summary>The provider ran past the question's time budget.</summary>
    Timeout = 3,

    /// <summary>The provider answered below the question's threshold.</summary>
    BelowThreshold = 4,
}
