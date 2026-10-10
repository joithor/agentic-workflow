using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1007 and AW1008: a class constructor's parameters are its dependencies (spec §4b.2). More than 5 is a split
/// signal (AW1008, advice); more than 7 is an error (AW1007). Every declared constructor is checked, primary or not.
/// Records are data shapes, not services, so their constructors aren't.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ConstructorDependencyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [AwDiagnostics.ConstructorDependencies, AwDiagnostics.ConstructorSplitSignal];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsRecord)
        {
            return;
        }

        foreach (var constructor in type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared && !c.Locations.IsEmpty))
        {
            var count = constructor.Parameters.Length;
            if (count > AwDiagnostics.MaxConstructorDependencies)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AwDiagnostics.ConstructorDependencies, constructor.Locations[0], type.Name, count, AwDiagnostics.MaxConstructorDependencies));
            }
            else if (count > AwDiagnostics.ConstructorSplitAt)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AwDiagnostics.ConstructorSplitSignal, constructor.Locations[0], type.Name, count, AwDiagnostics.ConstructorSplitAt));
            }
        }
    }
}
