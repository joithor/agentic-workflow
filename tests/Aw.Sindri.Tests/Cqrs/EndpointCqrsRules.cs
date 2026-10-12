using System.Reflection;
using System.Text.RegularExpressions;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>
/// ARCH-CQRS-001 (API spec §2.5) over the endpoint data source, since minimal-API lambdas are invisible to ArchUnitNET:
/// every GET resolves an IService whose request is an IQuery; every POST one whose request is an ICommand, or an IIssue
/// on one of the four carve-out routes with catalog rows of class 'issue'. Pure: each violation is one line.
/// The MCP endpoint (/sindri/mcp, API spec §8) is a transport, not a route: the SDK maps it, and each of its tools calls
/// one query service (Host/Mcp/McpToolTable), so it's skipped here.
/// </summary>
internal static partial class EndpointCqrsRules
{
    public const string Rule = "ARCH-CQRS-001";

    /// <summary>The routes an IIssue may take (API spec §2.5), with parameter names ignored.</summary>
    public static readonly IReadOnlyList<string> CarveOuts =
    [
        "POST /sindri/workflow/instances/{}/nonce",
        "POST /sindri/workflow/challenges",
        "POST /sindri/sessions/{}/heartbeat",
        "POST /sindri/sessions/{}/exit",
    ];

    public static IReadOnlyList<string> Check(EndpointDataSource endpoints, ICommandCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. endpoints.Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => !(endpoint.RoutePattern.RawText ?? string.Empty).StartsWith(Host.Mcp.McpEndpoint.Path, StringComparison.Ordinal))
            .SelectMany(endpoint => Violations(endpoint, catalog))];
    }

    /// <summary>The request type of the IService&lt;TRequest, _&gt; the endpoint's handler takes, or null.</summary>
    public static Type? Request(RouteEndpoint endpoint)
    {
        var parameters = endpoint.Metadata.OfType<IParameterBindingMetadata>().Select(p => p.ParameterInfo.ParameterType)
            .Concat(endpoint.Metadata.OfType<MethodInfo>().SelectMany(m => m.GetParameters()).Select(p => p.ParameterType));
        return parameters.FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>))?.GenericTypeArguments[0];
    }

    /// <summary>'METHOD /route' with parameter names dropped ('{itemId}' → '{}').</summary>
    public static IEnumerable<string> Routes(RouteEndpoint endpoint)
    {
        var route = Parameter.Replace(endpoint.RoutePattern.RawText ?? string.Empty, "{}");
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"];
        return methods.Select(method => method + " " + route);
    }

    private static IEnumerable<string> Violations(RouteEndpoint endpoint, ICommandCatalog catalog)
    {
        var request = Request(endpoint);
        foreach (var route in Routes(endpoint))
        {
            var problem = request is null ? "resolves no IService<TRequest, _>; bind one service and call it"
                : route.StartsWith("GET ", StringComparison.Ordinal) ? (Is(request, typeof(IQuery<>)) ? null : $"serves {request.Name}, which isn't an IQuery<>; a GET is a query")
                : route.StartsWith("POST ", StringComparison.Ordinal) ? Post(route, request, catalog)
                : "is neither a GET (query) nor a POST (command)";
            if (problem is not null)
            {
                yield return $"{Rule}: {route} {problem}";
            }
        }
    }

    private static string? Post(string route, Type request, ICommandCatalog catalog)
    {
        if (Is(request, typeof(ICommand<>)))
        {
            return null;
        }

        if (!Is(request, typeof(IIssue<>)))
        {
            return $"serves {request.Name}, which isn't an ICommand<> or IIssue<>; a POST is a command";
        }

        var rows = catalog.For(request);
        return !CarveOuts.Contains(route, StringComparer.Ordinal) ? $"serves the issue {request.Name} outside the four carve-out routes"
            : rows.Count == 0 || rows.Any(row => row.Class != ActionClass.Issue) ? $"serves {request.Name}, whose catalog rows aren't all of class 'issue'"
            : null;
    }

    private static bool Is(Type request, Type marker) =>
        request.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == marker);

    [GeneratedRegex("\\{[^}]*\\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Parameter { get; }
}
