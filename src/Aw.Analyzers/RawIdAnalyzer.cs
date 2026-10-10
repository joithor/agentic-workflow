using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1001: no raw ids in the public signatures of a 'Core' or 'Features' assembly (spec §4b.3): methods,
/// constructors, properties and indexers, fields, events and delegates. A raw id is a <see cref="Guid"/> anywhere in
/// the type (an array element or any type argument, so tuples and dictionaries too), or a <see cref="string"/> whose
/// member is named as an id ('id', '…Id', '…Ids'), also inside an array, a nullable or a one-argument generic such as
/// 'IReadOnlyList&lt;T&gt;'. A typed id's own members (it implements 'ITypedId&lt;,&gt;' or carries Vogen's
/// '[ValueObject]') wrap the primitive and are allowed. An override or interface implementation is reported at its
/// base when that base is reported (public, in this assembly, and raw itself); otherwise at the implementation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RawIdAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.RawId];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var name = start.Compilation.AssemblyName ?? string.Empty;
            if (!name.EndsWith(".Core", StringComparison.Ordinal) && !name.EndsWith(".Features", StringComparison.Ordinal))
            {
                return;
            }

            start.RegisterSymbolAction(
                c => Analyze(c, name),
                SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event, SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, string assembly)
    {
        var member = context.Symbol;
        if (member.IsImplicitlyDeclared || !IsPublicSurface(member) || IsTypedId(member as INamedTypeSymbol ?? member.ContainingType))
        {
            return;
        }

        var raw = RawSlots(member);
        if (raw.Count == 0 || InheritedShape.Bases(member).Any(b => ReportedAt(b, context.Compilation.Assembly)))
        {
            return;
        }

        foreach (var (kind, name, location) in raw)
        {
            context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.RawId, location, name, kind, assembly));
        }
    }

    // A base that this rule reports itself: public surface in this assembly, with a raw id in its own declaration.
    private static bool ReportedAt(ISymbol baseMember, IAssemblySymbol assembly) =>
        SymbolEqualityComparer.Default.Equals(baseMember.ContainingAssembly, assembly)
        && IsPublicSurface(baseMember)
        && RawSlots(baseMember.OriginalDefinition).Count > 0;

    // Every raw id in the member's signature: (kind, name, location).
    private static List<(string Kind, string Name, Location Location)> RawSlots(ISymbol member)
    {
        var slots = new List<(string, string, Location)>();
        void Add(ITypeSymbol type, string name, ImmutableArray<Location> locations)
        {
            var kind = RawIdKind(type, name);
            if (kind is not null && !locations.IsEmpty)
            {
                slots.Add((kind, name, locations[0]));
            }
        }

        foreach (var (type, name, locations) in Slots(member))
        {
            Add(type, name, locations);
        }

        return slots;
    }

    private static IEnumerable<(ITypeSymbol Type, string Name, ImmutableArray<Location> Locations)> Slots(ISymbol member) => member switch
    {
        IMethodSymbol method when method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor => MethodSlots(method, method.Name, method.Locations),
        IPropertySymbol property when !IsRecordPositional(property) =>
            property.Parameters.Select(p => (p.Type, p.Name, p.Locations)).Prepend((property.Type, property.Name, property.Locations)),
        IFieldSymbol field => [(field.Type, field.Name, field.Locations)],
        IEventSymbol @event => [(@event.Type, @event.Name, @event.Locations)],
        INamedTypeSymbol { DelegateInvokeMethod: { } invoke } => MethodSlots(invoke, invoke.Name, []),
        _ => [],
    };

    private static IEnumerable<(ITypeSymbol, string, ImmutableArray<Location>)> MethodSlots(
        IMethodSymbol method, string name, ImmutableArray<Location> returnLocations)
    {
        var parameters = method.Parameters.Select(p => (p.Type, p.Name, p.Locations));
        return method.ReturnsVoid ? parameters : parameters.Prepend((method.ReturnType, name, returnLocations));
    }

    // "Guid" or "string" when the type is a raw id under this member name, else null.
    private static string? RawIdKind(ITypeSymbol type, string name)
    {
        if (ContainsGuid(type))
        {
            return "Guid";
        }

        return Element(type).SpecialType == SpecialType.System_String && IsIdName(name) ? "string" : null;
    }

    // A Guid anywhere in the type: itself, an array element, or any type argument (tuples and dictionaries included).
    private static bool ContainsGuid(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => ContainsGuid(array.ElementType),
        INamedTypeSymbol named when IsNamed(named, "Guid", "System") => true,
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsGuid),
        _ => false,
    };

    private static ITypeSymbol Element(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => Element(array.ElementType),
        INamedTypeSymbol { TypeArguments.Length: 1 } generic => Element(generic.TypeArguments[0]),
        _ => type,
    };

    private static bool IsIdName(string name) =>
        string.Equals(name, "id", StringComparison.Ordinal)
        || string.Equals(name, "ids", StringComparison.Ordinal)
        || name.EndsWith("Id", StringComparison.Ordinal)
        || name.EndsWith("Ids", StringComparison.Ordinal);

    private static bool IsPublicSurface(ISymbol symbol)
    {
        for (var s = symbol; s is not null && s.Kind != SymbolKind.Namespace; s = s.ContainingSymbol)
        {
            if (s.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypedId(INamedTypeSymbol? type) =>
        type is not null
        && (type.AllInterfaces.Any(i => i.Arity == 2 && IsNamed(i, "ITypedId", "Aw.Kernel.Ids"))
            || type.GetAttributes().Any(a => a.AttributeClass is not null && IsNamed(a.AttributeClass, "ValueObjectAttribute", "Vogen")));

    private static bool IsNamed(ITypeSymbol type, string name, string ns) =>
        string.Equals(type.Name, name, StringComparison.Ordinal)
        && string.Equals(type.ContainingNamespace?.ToDisplayString(), ns, StringComparison.Ordinal);

    // A positional record parameter is also a property; report it once, on the parameter.
    private static bool IsRecordPositional(IPropertySymbol property) =>
        property.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is ParameterSyntax);
}
