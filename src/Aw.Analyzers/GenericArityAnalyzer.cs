using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1009: a generic type, delegate, method or local function declares at most 3 type parameters (spec §4b.3). An
/// override or interface implementation repeats its base's type parameters, so only the base declaration is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class GenericArityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.GenericArity];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(c => Check(c.ReportDiagnostic, c.Symbol), SymbolKind.NamedType, SymbolKind.Method);
        context.RegisterOperationAction(
            c => Check(c.ReportDiagnostic, ((Microsoft.CodeAnalysis.Operations.ILocalFunctionOperation)c.Operation).Symbol),
            OperationKind.LocalFunction);
    }

    private static void Check(Action<Diagnostic> report, ISymbol symbol)
    {
        var count = symbol switch
        {
            INamedTypeSymbol type => type.TypeParameters.Length,
            IMethodSymbol { IsImplicitlyDeclared: false } method => method.TypeParameters.Length,
            _ => 0,
        };
        if (count > AwDiagnostics.MaxTypeParameters && !symbol.Locations.IsEmpty
            && !InheritedShape.IsPartialImplementation(symbol) && !InheritedShape.IsInherited(symbol))
        {
            report(Diagnostic.Create(AwDiagnostics.GenericArity, symbol.Locations[0], symbol.Name, count, AwDiagnostics.MaxTypeParameters));
        }
    }
}
