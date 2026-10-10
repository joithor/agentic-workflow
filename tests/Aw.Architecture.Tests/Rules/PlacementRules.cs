using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Every type of a shaped package sits in a layer namespace (spec §4b.1): Core, Features, Infrastructure or Host, or in
/// the small shape App, App.Features, App.Infrastructure or App.Commands. A namespace under the root that names no layer,
/// the bare root namespace, or a namespace outside the root in a Core, Features or Infrastructure assembly would escape
/// the layer rules. (A type outside the root in a Host or App assembly, such as the top-level Program, is Host.)
/// </summary>
public static class PlacementRules
{
    /// <summary>A shaped package's type outside every layer namespace.</summary>
    public const string Id = "ARCH-PLACE-001";

    /// <summary>Checks that every type of a shaped package has a layer.</summary>
    /// <param name="types">The types to check.</param>
    /// <param name="scope">The shaped packages in scope.</param>
    /// <returns>One violation per offending namespace and assembly.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types, Scope scope) =>
        types
            .Where(t => !IsCompilerEmitted(t) && Place.Of(t, scope) is null && OwningRoot(t, scope) is not null)
            .Select(t => (Namespace: TypeNames.NamespaceOf(t), Assembly: TypeNames.AssemblyOf(t), Root: OwningRoot(t, scope)))
            .Distinct()
            .Select(p => new Violation(Id,
                $"namespace '{p.Namespace}' in {p.Assembly} names no layer of package {p.Root}",
                $"move the code into {p.Root}.Core, .Features.<Slice>, .Infrastructure or .Host (small shape: .App, "
                + ".App.Features.<Slice>, .App.Infrastructure or .App.Commands)"));

    // The package a type belongs to by namespace (under or at its root) or by its Core, Features or Infrastructure assembly.
    private static string? OwningRoot(IType type, Scope scope)
    {
        var ns = TypeNames.NamespaceOf(type);
        return scope.RootOf(ns)
            ?? scope.Roots.FirstOrDefault(r => r == ns)
            ?? scope.LayerRootOf(TypeNames.AssemblyOf(type));
    }

    // Types the compiler or a generator adds outside user namespaces: <PrivateImplementationDetails>, embedded attributes.
    private static bool IsCompilerEmitted(IType type)
    {
        var ns = TypeNames.NamespaceOf(type);
        return type.IsCompilerGenerated
            || TypeNames.TopLevel(type).StartsWith('<')
            || ns.StartsWith("System.", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal);
    }
}
