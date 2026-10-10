using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>Red stub.</summary>
public static class InfrastructureRules
{
    /// <summary>A public type in Infrastructure.</summary>
    public const string Id = "ARCH-INFRA-001";

    /// <summary>Red stub.</summary>
    /// <param name="types">The types.</param>
    /// <param name="roots">The roots.</param>
    /// <returns>Nothing yet.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types, IReadOnlyList<string> roots) =>
        types.Take(roots.Count * 0).Select(_ => new Violation(Id, string.Empty, string.Empty));
}
