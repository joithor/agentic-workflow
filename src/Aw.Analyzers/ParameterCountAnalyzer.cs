using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1006: a method, local function, indexer, operator, delegate or struct constructor has at most 5 parameters
/// (spec §4b.6). Not checked here: overrides and interface implementations (the base declaration owns the shape and
/// is checked there), class constructors (AW1007 and AW1008 own them), record constructors, positional or explicit
/// (a record is a data shape, not a call), and lambdas.
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
        if (method.Parameters.Length <= AwDiagnostics.MaxParameters || method.IsImplicitlyDeclared || InheritedShape.IsPartialImplementation(method))
        {
            return;
        }

        var checkedKind = method.MethodKind is MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion
            || (method.MethodKind == MethodKind.Constructor && IsNonClassConstructor(method));
        if (checkedKind && !InheritedShape.IsInherited(method))
        {
            var name = method.MethodKind == MethodKind.Constructor ? method.ContainingType.Name : method.Name;
            Report(context.ReportDiagnostic, name, method.Parameters.Length, method.Locations);
        }
    }

    // Class constructors belong to AW1007/AW1008; a record's constructors declare data, not a call. A struct's
    // constructors, primary or not, are counted here.
    private static bool IsNonClassConstructor(IMethodSymbol constructor) =>
        constructor.ContainingType is { IsRecord: false, TypeKind: TypeKind.Struct };

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
        if (property.IsIndexer && property.Parameters.Length > AwDiagnostics.MaxParameters
            && !InheritedShape.IsPartialImplementation(property) && !InheritedShape.IsInherited(property))
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
}
