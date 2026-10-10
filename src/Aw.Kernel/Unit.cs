namespace Aw.Kernel;

/// <summary>The single value of an operation that returns nothing but can still fail.</summary>
public readonly record struct Unit
{
    /// <summary>The only <see cref="Unit"/> value.</summary>
    public static Unit Value => default;
}
