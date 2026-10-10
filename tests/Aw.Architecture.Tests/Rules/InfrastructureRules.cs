using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>Infrastructure row and DTO types never leave the project (spec §4b.1).</summary>
public static class InfrastructureRules
{
    /// <summary>A public type in Infrastructure other than its registration class.</summary>
    public const string Id = "ARCH-INFRA-001";

    /// <summary>Checks that Infrastructure exposes only its *ServiceCollectionExtensions registration class.</summary>
    /// <param name="types">The types to check.</param>
    /// <param name="scope">The shaped packages in scope.</param>
    /// <returns>One violation per public Infrastructure type.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types, Scope scope) =>
        types
            .Where(t => !t.IsNested && t.Visibility == Visibility.Public
                && Place.Of(t, scope) is { Layer: Layer.Infrastructure }
                && !TypeNames.TopLevel(t).EndsWith("ServiceCollectionExtensions", StringComparison.Ordinal))
            .Select(t => new Violation(Id, $"{t.FullName} is public in Infrastructure",
                "make it internal: rows, DTOs, repositories and adapters stay inside Infrastructure, which returns Core types"));
}
