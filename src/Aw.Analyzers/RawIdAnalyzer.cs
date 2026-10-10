using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1001: no raw ids in the public signatures of a 'Core' or 'Features' assembly (spec §4b.3). A raw id is
/// a <see cref="Guid"/> anywhere, or a <see cref="string"/> whose member is named as an id ('id', '…Id',
/// '…Ids'), also inside an array, a nullable or a one-argument generic such as 'IReadOnlyList&lt;T&gt;'. A typed
/// id's own members (it implements 'ITypedId&lt;,&gt;' or carries Vogen's '[ValueObject]') wrap the primitive
/// and are allowed.
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
        });
    }

    private static void AnalyzeMember(SymbolAnalysisContext context, string assembly)
    {
        var member = context.Symbol;
        if (member.IsImplicitlyDeclared || !IsPublicSurface(member) || IsTypedId(member.ContainingType))
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
        var element = Element(type);
        if (element.SpecialType == SpecialType.System_String)
        {
            return IsIdName(name) ? "string" : null;
        }

        return IsNamed(element, "Guid", "System") ? "Guid" : null;
    }

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
