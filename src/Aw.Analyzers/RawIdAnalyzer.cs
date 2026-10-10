using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1001: no raw ids in the public signatures of a 'Core' or 'Features' assembly (spec §4b.3): methods,
/// constructors, operators and conversions, properties and indexers, fields, events, delegates and extension-block
/// receivers. A raw id is a <see cref="Guid"/> anywhere in
/// the type (an array element or any type argument, so tuples and dictionaries too), or a <see cref="string"/> whose
/// member is named as an id ('id', '…Id', '…Ids'), also inside an array, a nullable or a one-argument generic such as
/// 'IReadOnlyList&lt;T&gt;'. A typed id's own members (it implements 'ITypedId&lt;,&gt;' or carries Vogen's
/// '[ValueObject]') wrap the primitive and are allowed. A slot of an override or interface implementation (its
/// return or a parameter) is reported at the base when the base reports the same slot (public, in this assembly, raw
/// in its own declaration, with no Guid added by its type arguments); otherwise at the implementation. A partial
/// member is reported once (at its defining part; a partial constructor at its implementing part, the only one Roslyn
/// analyzes). An extension block's receiver counts when the block has a public member.
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
        if (member.IsImplicitlyDeclared || InheritedShape.IsPartialImplementation(member) || !IsPublicSurface(member)
            || IsTypedId(member as INamedTypeSymbol ?? member.ContainingType))
        {
            return;
        }

        var raw = RawSlots(member);
        if (raw.Count == 0)
        {
            return;
        }

        // A slot its base reports too (the same position, raw in the base's own declaration, and no Guid added by the
        // base's type arguments) is reported there only.
        var reportedAtBase = new HashSet<int>(InheritedShape.Bases(member)
            .Where(b => SymbolEqualityComparer.Default.Equals(b.ContainingAssembly, context.Compilation.Assembly) && IsPublicSurface(b))
            .SelectMany(ReportedPositions));
        foreach (var slot in raw.Where(slot => !reportedAtBase.Contains(slot.Position)))
        {
            context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.RawId, slot.Location, slot.Name, slot.Kind, assembly));
        }
    }

    // The positions a base reports in its own declaration whose constructed type (through the implementer's type
    // arguments) holds no more Guids than the declaration does.
    private static IEnumerable<int> ReportedPositions(ISymbol baseMember)
    {
        var constructed = Slots(baseMember).ToDictionary(s => s.Position, s => s.Type);
        var declared = Slots(baseMember.OriginalDefinition).ToDictionary(s => s.Position, s => s.Type);
        return RawSlots(baseMember.OriginalDefinition)
            .Select(slot => slot.Position)
            .Where(p => constructed.TryGetValue(p, out var c) && declared.TryGetValue(p, out var d) && GuidCount(c) == GuidCount(d));
    }

    private static int GuidCount(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => GuidCount(array.ElementType),
        INamedTypeSymbol named when IsNamed(named, "Guid", "System") => 1,
        INamedTypeSymbol named => named.TypeArguments.Sum(GuidCount),
        _ => 0,
    };

    // Every raw id in the member's signature. Position: -1 for the member's own type (return, property, field,
    // event), i for parameter i, so an implementation's slots line up with its base's.
    private static List<(int Position, string Kind, string Name, Location Location)> RawSlots(ISymbol member)
    {
        var slots = new List<(int, string, string, Location)>();
        foreach (var (position, type, name, locations) in Slots(member))
        {
            var kind = RawIdKind(type, name);
            if (kind is not null && !locations.IsEmpty)
            {
                slots.Add((position, kind, name, locations[0]));
            }
        }

        return slots;
    }

    private static IEnumerable<(int Position, ITypeSymbol Type, string Name, ImmutableArray<Location> Locations)> Slots(ISymbol member) => member switch
    {
        IMethodSymbol method when method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator =>
            MethodSlots(method, method.Name, method.Locations),
        IMethodSymbol { MethodKind: MethodKind.Conversion } conversion => MethodSlots(conversion, conversion.ContainingType.Name, conversion.Locations),
        INamedTypeSymbol { IsExtension: true, ExtensionParameter: { } receiver } extension
            when extension.GetMembers().Any(m => !m.IsImplicitlyDeclared && IsPublicSurface(m)) =>
            [(0, receiver.Type, receiver.Name, receiver.Locations)],
        IPropertySymbol property when !IsRecordPositional(property) =>
            Parameters(property.Parameters).Prepend((-1, property.Type, property.Name, property.Locations)),
        // A const can never be a typed id, so a const string named like an id (a rule id, PKT-ID-001) has no fix.
        IFieldSymbol { IsConst: true } => [],
        IFieldSymbol field => [(-1, field.Type, field.Name, field.Locations)],
        IEventSymbol @event => [(-1, @event.Type, @event.Name, @event.Locations)],
        INamedTypeSymbol { DelegateInvokeMethod: { } invoke } type => MethodSlots(invoke, type.Name, type.Locations),
        _ => [],
    };

    private static IEnumerable<(int, ITypeSymbol, string, ImmutableArray<Location>)> MethodSlots(
        IMethodSymbol method, string name, ImmutableArray<Location> returnLocations) =>
        method.ReturnsVoid
            ? Parameters(method.Parameters)
            : Parameters(method.Parameters).Prepend((-1, method.ReturnType, name, returnLocations));

    private static IEnumerable<(int, ITypeSymbol, string, ImmutableArray<Location>)> Parameters(ImmutableArray<IParameterSymbol> parameters) =>
        parameters.Select(p => (p.Ordinal, p.Type, p.Name, p.Locations));

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
