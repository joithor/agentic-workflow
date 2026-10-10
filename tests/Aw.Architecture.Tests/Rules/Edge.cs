using ArchUnitNET.Domain;

namespace Aw.Architecture.Tests.Rules;

/// <summary>One type-to-type dependency with both ends placed in the package shape.</summary>
/// <param name="Origin">The depending type.</param>
/// <param name="From">The origin's place, or null outside a shaped package.</param>
/// <param name="Target">The type depended on.</param>
/// <param name="To">The target's place, or null outside a shaped package.</param>
public sealed record Edge(IType Origin, Place? From, IType Target, Place? To)
{
    /// <summary>Every dependency of the given types, deduplicated by top-level origin and target.</summary>
    /// <param name="types">The types whose dependencies to list.</param>
    /// <param name="roots">The package roots in scope.</param>
    /// <returns>The edges.</returns>
    public static IReadOnlyList<Edge> All(IEnumerable<IType> types, IReadOnlyList<string> roots) =>
        [.. types
            .SelectMany(t => t.Dependencies.Select(d => (Origin: t, d.Target)))
            .Where(p => !ReferenceEquals(p.Origin, p.Target) && p.Target is not GenericParameter)
            .DistinctBy(p => (TypeNames.Qualified(p.Origin), p.Target.FullName))
            .Select(p => new Edge(p.Origin, Place.Of(p.Origin, roots), p.Target, Place.Of(p.Target, roots)))];

    /// <summary>"Origin -> Target" for messages.</summary>
    public string Text => $"{TypeNames.Qualified(Origin)} -> {Target.FullName}";
}
