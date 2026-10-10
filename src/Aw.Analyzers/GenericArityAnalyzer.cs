using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1009: a generic type, delegate or method declares at most 3 type parameters (spec §4b.3). An override repeats
/// its base method's type parameters, so only the base declaration is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class GenericArityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.GenericArity];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType, SymbolKind.Method);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var count = context.Symbol switch
        {
            INamedTypeSymbol type => type.TypeParameters.Length,
            IMethodSymbol { IsImplicitlyDeclared: false, IsOverride: false } method => method.TypeParameters.Length,
            _ => 0,
        };
        if (count > AwDiagnostics.MaxTypeParameters && !context.Symbol.Locations.IsEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AwDiagnostics.GenericArity, context.Symbol.Locations[0], context.Symbol.Name, count, AwDiagnostics.MaxTypeParameters));
        }
    }
}
