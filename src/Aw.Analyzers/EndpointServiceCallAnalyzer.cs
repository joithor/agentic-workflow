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
    private const string ServiceInterface = "Aw.Kernel.Services.IService`2";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.EndpointServiceCalls];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Only a project that sees both types can hold an endpoint this rule checks (in practice, a Host).
            var map = start.Compilation.GetTypeByMetadataName(MapExtensions);
            var service = start.Compilation.GetTypeByMetadataName(ServiceInterface);
            if (map is not null && service is not null)
            {
                start.RegisterSyntaxNodeAction(c => Analyze(c, map, service), SyntaxKind.InvocationExpression);
            }
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol map, INamedTypeSymbol service)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!InvokedName(invocation).StartsWith("Map", StringComparison.Ordinal)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, map))
        {
            return;
        }

        foreach (var lambda in invocation.ArgumentList.Arguments.Select(a => a.Expression).OfType<AnonymousFunctionExpressionSyntax>())
        {
            var calls = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().Count(c => IsServiceHandle(context, c, service));
            if (calls != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.EndpointServiceCalls, lambda.GetLocation(), calls));
            }
        }
    }

    private static string InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => string.Empty,
    };

    private static bool IsServiceHandle(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax call, INamedTypeSymbol service) =>
        string.Equals(InvokedName(call), "Handle", StringComparison.Ordinal)
        && context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol method
        && SymbolEqualityComparer.Default.Equals(method.ContainingType?.OriginalDefinition, service);
}
