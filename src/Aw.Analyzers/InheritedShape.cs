using Microsoft.CodeAnalysis;

namespace Aw.Analyzers;

/// <summary>
/// Whether a member's signature is set by a base declaration (an override or an interface implementation). The
/// shape rules report the base declaration only: the implementer can't change the shape in place.
/// </summary>
internal static class InheritedShape
{
    internal static bool IsInherited(ISymbol member) => member switch
    {
        IMethodSymbol method => method.IsOverride || !method.ExplicitInterfaceImplementations.IsEmpty || ImplementsInterface(method),
        IPropertySymbol property => property.IsOverride || !property.ExplicitInterfaceImplementations.IsEmpty || ImplementsInterface(property),
        _ => false,
    };

    private static bool ImplementsInterface(ISymbol member) =>
        member.ContainingType is { } type
        && type.AllInterfaces.SelectMany(i => i.GetMembers(member.Name))
            .Any(m => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(m), member));
}
