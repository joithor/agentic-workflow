namespace Aw.Architecture.Tests.Rules;

/// <summary>The dependency direction inside and between packages (spec §4b.1). Red: not implemented yet.</summary>
public static class DirectionRules
{
    /// <summary>Core depends on nothing else in its package.</summary>
    public const string CoreId = "ARCH-DIR-001";

    /// <summary>Features depends only on Core and Features.</summary>
    public const string FeaturesId = "ARCH-DIR-002";

    /// <summary>Infrastructure depends only on Core and Infrastructure.</summary>
    public const string InfrastructureId = "ARCH-DIR-003";

    /// <summary>Another package is used only through its Core.</summary>
    public const string CrossPackageId = "ARCH-DIR-004";

    /// <summary>Red stub.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) => edges.Take(0).Select(_ => new Violation(CoreId, string.Empty, string.Empty));
}
