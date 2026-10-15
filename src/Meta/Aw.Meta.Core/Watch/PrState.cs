namespace Aw.Meta.Core.Watch;

/// <summary>A pull request's lifecycle state.</summary>
public enum PrState
{
    /// <summary>Open.</summary>
    Open = 1,

    /// <summary>Closed without merging.</summary>
    Closed = 2,

    /// <summary>Merged.</summary>
    Merged = 3,
}
