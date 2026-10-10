namespace Aw.Kernel.Testing.Tests;

/// <summary>The builder root a package's Testing project ships: 'A.Item().WithSize(Size.L).Build()'.</summary>
public static class A
{
    /// <summary>A builder with deterministic defaults.</summary>
    /// <returns>The builder.</returns>
    public static ItemBuilder Item() => new();
}
