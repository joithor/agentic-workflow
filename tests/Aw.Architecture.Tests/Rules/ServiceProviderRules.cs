namespace Aw.Architecture.Tests.Rules;

/// <summary>Red stub.</summary>
public static class ServiceProviderRules
{
    /// <summary>The rule id.</summary>
    public const string Id = "ARCH-DI-001";

    /// <summary>Red stub.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) => edges.Take(0).Select(_ => new Violation(Id, string.Empty, string.Empty));
}
