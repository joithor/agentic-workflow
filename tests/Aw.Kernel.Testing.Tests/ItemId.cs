namespace Aw.Kernel.Testing.Tests;

/// <summary>A sample strongly typed id, the way a package's Core declares one.</summary>
/// <param name="Value">The underlying id.</param>
public readonly record struct ItemId(Guid Value)
{
    /// <summary>The id a <see cref="SequentialIdGenerator"/> issues n-th.</summary>
    /// <param name="n">1-based.</param>
    /// <returns>The id.</returns>
    public static ItemId From(long n) => new(SequentialIdGenerator.IdAt(n));
}
