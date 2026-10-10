using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1003: a slice never names another slice's types (spec §4b.1). A slice is the namespace segment after
/// 'Features' ('Aw.&lt;Pkg&gt;.Features.&lt;Slice&gt;', or 'Aw.&lt;Pkg&gt;.App.Features.&lt;Slice&gt;' in the
/// small shape). Each reference is reported once, on the type's own name (attributes included; 'var' names no type).
/// This is the fast check in the editor and the build; the ArchUnitNET test is authoritative.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CrossSliceAnalyzer : DiagnosticAnalyzer
{
    private const string FeaturesSegment = "Features";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.CrossSlice];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var from = context.ContainingSymbol is null ? null : Slice(Namespace(context.ContainingSymbol));
        if (from is null)
        {
            return;
        }

        var name = (SimpleNameSyntax)context.Node;
        if (name.IsVar || IsQualifierOfNestedType(context, name))
        {
            return;
        }

        var type = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol switch
        {
            INamedTypeSymbol named => named,
            IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => constructor.ContainingType,   // an attribute name binds to its constructor
            _ => null,
        };
        if (type is null || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly))
        {
            return;
        }

        var to = Slice(Namespace(type));
        if (to is not null && to.Value.Root == from.Value.Root && to.Value.Name != from.Value.Name)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AwDiagnostics.CrossSlice, name.GetLocation(), from.Value.Name, type.Name, to.Value.Name));
        }
    }

    // 'Rules' in 'Rules.Inner': the nested type is the reference, reported on its own name.
    private static bool IsQualifierOfNestedType(SyntaxNodeAnalysisContext context, SimpleNameSyntax name)
    {
        // The whole qualified expression this name ends ('B.Rules' for 'Rules'), then what qualifies it.
        SyntaxNode self = name.Parent switch
        {
            QualifiedNameSyntax q when q.Right == name => q,
            MemberAccessExpressionSyntax m when m.Name == name => m,
            _ => name,
        };
        var right = self.Parent switch
        {
            QualifiedNameSyntax q when q.Left == self => q.Right,
            MemberAccessExpressionSyntax m when m.Expression == self => m.Name,
            _ => null,
        };
        return right is not null
            && context.SemanticModel.GetSymbolInfo(right, context.CancellationToken).Symbol is INamedTypeSymbol;
    }

    private static string Namespace(ISymbol symbol) =>
        symbol is INamespaceSymbol ns ? ns.ToDisplayString() : symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

    // ("Aw.Pkg.Features", "StartItem") for "Aw.Pkg.Features.StartItem[.…]", else null.
    private static (string Root, string Name)? Slice(string ns)
    {
        var parts = ns.Split('.');
        var at = Array.IndexOf(parts, FeaturesSegment);
        return at < 0 || at + 1 >= parts.Length
            ? null
            : (string.Join(".", parts, 0, at + 1), parts[at + 1]);
    }
}
