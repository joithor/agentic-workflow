using Microsoft.CodeAnalysis;

namespace Aw.Analyzers;

/// <summary>
/// The base declarations that set a member's signature: what it overrides and the interface members it implements.
/// The shape rules report the base declaration: the implementer can't change the shape in place.
/// </summary>
internal static class InheritedShape
{
    internal static bool IsInherited(ISymbol member) => Bases(member).Any();

    /// <summary>
    /// The implementing part of a partial member repeats the defining part's shape; rules report the definition. A
    /// partial constructor is the exception: Roslyn hands analyzers only its implementing part, so that part is kept.
    /// </summary>
    internal static bool IsPartialImplementation(ISymbol member) => member switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor } => false,
        IMethodSymbol method => method.PartialDefinitionPart is not null,
        IPropertySymbol property => property.PartialDefinitionPart is not null,
        IEventSymbol @event => @event.PartialDefinitionPart is not null,
        _ => false,
    };

    internal static IEnumerable<ISymbol> Bases(ISymbol member)
    {
        var overridden = member switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => (ISymbol?)null,
        };
        if (overridden is not null)
        {
            yield return overridden;
        }

        if (member.ContainingType is not { } type)
        {
            yield break;
        }

        // Explicit implementations are found here too: FindImplementationForInterfaceMember returns them.
        foreach (var candidate in type.AllInterfaces.SelectMany(i => i.GetMembers()))
        {
            if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(candidate), member))
            {
                yield return candidate;
            }
        }
    }
}
