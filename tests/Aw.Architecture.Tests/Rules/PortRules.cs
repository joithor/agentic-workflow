namespace Aw.Architecture.Tests.Rules;

/// <summary>Red stub.</summary>
public static class PortRules
{
    /// <summary>A technology type in Core, Features or Commands.</summary>
    public const string TechnologyId = "ARCH-PORT-001";

    /// <summary>Infrastructure used outside Composition.</summary>
    public const string CompositionId = "ARCH-PORT-002";

    /// <summary>Red stub.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) => edges.Take(0).Select(_ => new Violation(TechnologyId, string.Empty, string.Empty));
}
