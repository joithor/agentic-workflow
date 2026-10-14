using System.Diagnostics;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// Wraps the host's real context engine and records, for each query, the engine caller Sindri built and the span the call
/// ran inside, so a test can show an MCP call took the same query chain as the HTTP route.
/// </summary>
internal sealed class RecordingEngine(IContextEngine inner, List<(ContextCaller Caller, string? Span)> calls) : IContextEngine
{
    /// <summary>Replaces the registered engine with one that records into <paramref name="calls"/>.</summary>
    public static void Install(IServiceCollection services, List<(ContextCaller Caller, string? Span)> calls)
    {
        var original = services.Last(d => d.ServiceType == typeof(IContextEngine));
        _ = services.Remove(original);
        services.Add(new ServiceDescriptor(typeof(IContextEngine), sp => new RecordingEngine(Resolve(original, sp), calls), original.Lifetime));
    }

    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) => inner.CheckSettings(section, repos);

    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken)
    {
        lock (calls)
        {
            calls.Add((scope.Caller, Activity.Current?.DisplayName));
        }

        return inner.Query(query, scope, cancellationToken);
    }

    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) => inner.Sources(scope, cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) => inner.Refresh(scope, cancellationToken);

    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) => inner.PreviewMap(path, scope, cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) =>
        inner.ImportMap(path, confirmedHash, scope, cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) => inner.ExportMap(path, scope, cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(ContextScope scope, CancellationToken cancellationToken) => inner.MapStatus(scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(CancellationToken cancellationToken) => inner.CheckMap(cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) => inner.CheckRetention(cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) => inner.PurgeExpired(cancellationToken);

    private static IContextEngine Resolve(ServiceDescriptor original, IServiceProvider services) =>
        (IContextEngine)(original.ImplementationInstance
            ?? original.ImplementationFactory?.Invoke(services)
            ?? ActivatorUtilities.CreateInstance(services, original.ImplementationType!));   // why: a descriptor has one of the three
}
