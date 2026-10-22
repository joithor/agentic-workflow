namespace Aw.CodeIndex.Core.Facade;

/// <summary>How many levels a callers or callees walk follows (spec §7: 1 to 3).</summary>
public enum CallDepth
{
    /// <summary>Direct callers or callees only.</summary>
    One = 1,

    /// <summary>Two levels.</summary>
    Two = 2,

    /// <summary>Three levels.</summary>
    Three = 3,
}
