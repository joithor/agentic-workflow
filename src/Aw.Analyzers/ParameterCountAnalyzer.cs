using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1006: a method, local function, indexer, operator or delegate has at most 5 parameters (spec §4b.6). Not
/// checked: overrides and interface implementations (the base declaration owns the shape and is checked there),
/// constructors (AW1007 owns them), record positional parameters (data, not a call) and lambdas.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ParameterCountAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.ParameterCount];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeIndexer, SymbolKind.Property);
        context.RegisterSymbolAction(AnalyzeDelegate, SymbolKind.NamedType);
        context.RegisterOperationAction(AnalyzeLocalFunction, OperationKind.LocalFunction);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        var checkedKind = method.MethodKind is MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion;
        if (checkedKind && !method.IsImplicitlyDeclared && !IsInherited(method))
        {
            Report(context.ReportDiagnostic, method.Name, method.Parameters.Length, method.Locations);
        }
    }

    // A delegate's parameters are those of its (compiler-declared) Invoke method.
    private static void AnalyzeDelegate(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.DelegateInvokeMethod is { } invoke)
        {
            Report(context.ReportDiagnostic, type.Name, invoke.Parameters.Length, type.Locations);
        }
    }

    private static void AnalyzeIndexer(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.IsIndexer && !property.IsOverride && property.ExplicitInterfaceImplementations.IsEmpty && !ImplementsInterface(property))
        {
            Report(context.ReportDiagnostic, "this[]", property.Parameters.Length, property.Locations);
        }
    }

    private static void AnalyzeLocalFunction(OperationAnalysisContext context)
    {
        var function = ((Microsoft.CodeAnalysis.Operations.ILocalFunctionOperation)context.Operation).Symbol;
        Report(context.ReportDiagnostic, function.Name, function.Parameters.Length, function.Locations);
    }

    private static void Report(Action<Diagnostic> report, string name, int count, ImmutableArray<Location> locations)
    {
        if (count > AwDiagnostics.MaxParameters && !locations.IsEmpty)
        {
            report(Diagnostic.Create(AwDiagnostics.ParameterCount, locations[0], name, count, AwDiagnostics.MaxParameters));
        }
    }

    private static bool IsInherited(IMethodSymbol method) =>
        method.IsOverride || !method.ExplicitInterfaceImplementations.IsEmpty || ImplementsInterface(method);

    private static bool ImplementsInterface(ISymbol member) =>
        member.ContainingType is { } type
        && type.AllInterfaces.SelectMany(i => i.GetMembers(member.Name))
            .Any(m => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(m), member));
}
