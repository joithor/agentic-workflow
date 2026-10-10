using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1001: no raw ids in the public signatures of a 'Core' or 'Features' assembly (spec §4b.3): methods,
/// constructors, properties, fields and delegates. A raw id is a <see cref="Guid"/> anywhere in the type (an array
/// element or any type argument, so tuples and dictionaries too), or a <see cref="string"/> whose member is named as
/// an id ('id', '…Id', '…Ids'), also inside an array, a nullable or a one-argument generic such as
/// 'IReadOnlyList&lt;T&gt;'. A typed id's own members (it implements 'ITypedId&lt;,&gt;' or carries Vogen's
/// '[ValueObject]') wrap the primitive and are allowed. Overrides and interface implementations are reported at
/// their base declaration.
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

            start.RegisterSymbolAction(c => AnalyzeMember(c, name), SymbolKind.Method, SymbolKind.Property, SymbolKind.Field);
            start.RegisterSymbolAction(c => AnalyzeDelegate(c, name), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeMember(SymbolAnalysisContext context, string assembly)
    {
        var member = context.Symbol;
        if (!InScope(member))
        {
            return;
        }

        switch (member)
        {
            case IMethodSymbol method when method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor:
                if (!method.ReturnsVoid)
                {
                    Check(context, method.ReturnType, method.Name, method.Locations, assembly);
                }

                foreach (var parameter in method.Parameters)
                {
                    Check(context, parameter.Type, parameter.Name, parameter.Locations, assembly);
                }

                break;
            case IPropertySymbol property when !IsRecordPositional(property):
                Check(context, property.Type, property.Name, property.Locations, assembly);
                break;
            case IFieldSymbol field:
                Check(context, field.Type, field.Name, field.Locations, assembly);
                break;
            default:
                break;
        }
    }

    private static bool InScope(ISymbol member) =>
        !member.IsImplicitlyDeclared && IsPublicSurface(member) && !IsTypedId(member.ContainingType) && !InheritedShape.IsInherited(member);

    // A delegate's signature is its (compiler-declared) Invoke method.
    private static void AnalyzeDelegate(SymbolAnalysisContext context, string assembly)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.DelegateInvokeMethod is not { } invoke || !IsPublicSurface(type))
        {
            return;
        }

        foreach (var parameter in invoke.Parameters)
        {
            Check(context, parameter.Type, parameter.Name, parameter.Locations, assembly);
        }
    }

    private static void Check(SymbolAnalysisContext context, ITypeSymbol type, string name, ImmutableArray<Location> locations, string assembly)
    {
        var raw = RawIdKind(type, name);
        if (raw is not null && !locations.IsEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.RawId, locations[0], name, raw, assembly));
        }
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
