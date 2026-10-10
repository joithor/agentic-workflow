using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1004: a minimal-API endpoint lambda (a lambda passed to an 'EndpointRouteBuilderExtensions.Map…' method)
/// calls 'IService&lt;TRequest, TResponse&gt;.Handle' exactly once (spec §4b.2): it binds input, calls one
/// service and maps the result. Method-group handlers aren't checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class EndpointServiceCallAnalyzer : DiagnosticAnalyzer
{
    private const string MapExtensions = "Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions";
    private const string ServiceInterface = "Aw.Kernel.Services.IService<TRequest, TResponse>";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.EndpointServiceCalls];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol map
            || !map.Name.StartsWith("Map", StringComparison.Ordinal)
            || !string.Equals(map.ContainingType?.ToDisplayString(), MapExtensions, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var lambda in invocation.ArgumentList.Arguments.Select(a => a.Expression).OfType<AnonymousFunctionExpressionSyntax>())
        {
            var calls = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(c => IsServiceHandle(context, c));
            if (calls != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.EndpointServiceCalls, lambda.GetLocation(), calls));
            }
        }
    }

    private static bool IsServiceHandle(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax call) =>
        context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol method
        && string.Equals(method.Name, "Handle", StringComparison.Ordinal)
        && string.Equals(method.ContainingType?.OriginalDefinition.ToDisplayString(), ServiceInterface, StringComparison.Ordinal);
}
