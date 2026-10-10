using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Every namespace under a package root names a layer (spec §4b.1): Core, Features, Infrastructure or Host, or in the
/// small shape App, App.Features, App.Infrastructure or App.Commands. Anything else would escape the layer rules.
/// </summary>
public static class PlacementRules
{
    /// <summary>A namespace under a package root that names no layer.</summary>
    public const string Id = "ARCH-PLACE-001";

    /// <summary>Checks that every type under a package root has a layer.</summary>
    /// <param name="types">The types to check.</param>
    /// <param name="roots">The package roots in scope.</param>
    /// <returns>One violation per offending namespace.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types, IReadOnlyList<string> roots) =>
        types
            .Where(t => Place.RootOf(TypeNames.NamespaceOf(t), roots) is not null && Place.Of(t, roots) is null)
            .Select(TypeNames.NamespaceOf)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => new Violation(Id, $"namespace {ns} names no layer",
                "move the code into Core, Features/<Slice>, Infrastructure or Host (small shape: App, App.Features/<Slice>, "
                + "App.Infrastructure or App.Commands)"));
}
