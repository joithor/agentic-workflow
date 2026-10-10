using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Every type of a shaped package sits in a layer namespace (spec §4b.1): Core, Features, Infrastructure or Host, or in
/// the small shape App, App.Features, App.Infrastructure or App.Commands. A namespace under the root that names no layer,
/// the bare root namespace, a namespace outside the root in a Core, Features or Infrastructure assembly, or a namespace
/// naming another layer than its assembly's (Aw.X.Infrastructure.Http inside Aw.X.Core) would escape the layer rules.
/// A type outside the root in a Host or App assembly, such as the top-level Program, is Host. IDE0130 (namespace
/// matches folder) keeps most of this from compiling; this rule is the backstop.
/// </summary>
public static class PlacementRules
{
    /// <summary>A shaped package's type outside its layer namespace.</summary>
    public const string Id = "ARCH-PLACE-001";

    private const string Fix = "put the type in its project's layer namespace: <Pkg>.Core, .Features.<Slice>, .Infrastructure or .Host "
        + "(small shape: .App, .App.Features.<Slice>, .App.Infrastructure or .App.Commands), matching the project it is in";

    /// <summary>Checks that every type of a shaped package sits in its layer.</summary>
    /// <param name="types">The types to check.</param>
    /// <param name="scope">The shaped packages in scope.</param>
    /// <returns>One violation per offending namespace and assembly.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<IType> types, Scope scope) =>
        types
            // Defensive: ArchUnitNET 0.13.4 already drops compiler output (<PrivateImplementationDetails>, <>c, embedded
            // attributes) when it loads an assembly, so no test can reach this filter with a real type; it keeps such a
            // type, should a later ArchUnitNET keep it, from being reported as off-root. IsCompilerEmitted is tested alone.
            .Where(t => !IsCompilerEmitted(TypeNames.TopLevel(t), t.IsCompilerGenerated, t.Attributes.Select(a => a.FullName)))
            .Select(t => Judge(t, scope))
            .OfType<Violation>()
            .Distinct();

    /// <summary>Whether the compiler emitted a type: a &lt;...&gt; name, [CompilerGenerated] or [Microsoft.CodeAnalysis.Embedded].</summary>
    /// <param name="topLevelName">The top-level type name.</param>
    /// <param name="compilerGenerated">Whether the type carries [CompilerGenerated].</param>
    /// <param name="attributes">The full names of its attributes.</param>
    /// <returns>True for compiler output.</returns>
    public static bool IsCompilerEmitted(string topLevelName, bool compilerGenerated, IEnumerable<string> attributes) =>
        compilerGenerated
        || topLevelName.StartsWith('<')
        || attributes.Contains("Microsoft.CodeAnalysis.EmbeddedAttribute", StringComparer.Ordinal);

    private static Violation? Judge(IType type, Scope scope)
    {
        var ns = TypeNames.NamespaceOf(type);
        var assembly = TypeNames.AssemblyOf(type);
        var place = Place.Of(type, scope);
        var layerAssembly = scope.LayerOf(assembly);
        if (place is null)
        {
            var root = scope.RootOf(ns) ?? scope.Roots.FirstOrDefault(r => r == ns) ?? layerAssembly?.Root;
            return root is null ? null : new Violation(Id, $"namespace '{ns}' in {assembly} names no layer of package {root}", Fix);
        }

        return layerAssembly is { } held && (held.Root != place.Root || held.Layer != place.Layer)
            ? new Violation(Id, $"namespace '{ns}' in {assembly} is {place.Root} {place.Layer}, but {assembly} holds {held.Root} {held.Layer}", Fix)
            : null;
    }
}
