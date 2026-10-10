using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests.Fixtures;

/// <summary>The fixture packages under Fixtures/: each folder is one package root that breaks (or keeps) a rule.</summary>
internal static class FixtureSet
{
    private const string Prefix = "Aw.Architecture.Tests.Fixtures.";

    private static readonly Lazy<IReadOnlyList<IType>> _assembly = new(() =>
        [.. new ArchLoader().LoadAssemblies(typeof(FixtureSet).Assembly).Build().Types]);

    private static readonly Lazy<IReadOnlyList<IType>> _all = new(() =>
        [.. _assembly.Value.Where(t => TypeNames.NamespaceOf(t).StartsWith(Prefix, StringComparison.Ordinal))]);

    /// <summary>A type of this test assembly by full name, from the ArchUnitNET model.</summary>
    /// <param name="fullName">The type's full name.</param>
    /// <returns>The type.</returns>
    public static IType Named(string fullName) => _assembly.Value.Single(t => t.FullName == fullName);

    /// <summary>The types of the named fixture packages.</summary>
    /// <param name="packages">Fixture folder names, e.g. Good.</param>
    /// <returns>Their types.</returns>
    public static IReadOnlyList<IType> Types(params string[] packages) =>
        [.. _all.Value.Where(t => packages.Any(p => TypeNames.NamespaceOf(t) == Prefix + p
            || TypeNames.NamespaceOf(t).StartsWith(Prefix + p + ".", StringComparison.Ordinal)))];

    /// <summary>The package roots of the named fixture packages.</summary>
    /// <param name="packages">Fixture folder names.</param>
    /// <returns>Their roots.</returns>
    public static Scope Roots(params string[] packages) => Scope.Of(packages.Select(p => Prefix + p));

    /// <summary>The dependency edges among the named fixture packages' types.</summary>
    /// <param name="packages">Fixture folder names.</param>
    /// <returns>The edges.</returns>
    public static IReadOnlyList<Edge> Edges(params string[] packages) => Edge.All(Types(packages), Roots(packages));
}
