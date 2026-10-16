using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the context engine's two jobs on the P6 job runtime: 'SyncSources' and 'BuildContextMap'.</summary>
internal static class ContextJobServiceCollectionExtensions
{
    /// <summary>Adds both jobs, each keyed by a constant: one sync at a time, whatever it names, and one build at a time (the profile's one map).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddContextJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddJob<SyncContextSourcesRequest, SyncContextSourcesResult, SyncContextSourcesService>(
                static _ => JobKey.From("sync"), static r => SyncSummary(r), PrecheckSync)
            .AddJob<BuildContextMapRequest, BuildContextMapResult, BuildContextMapService>(
                static _ => JobKey.From("build"), static r => string.Create(CultureInfo.InvariantCulture, $"draft v{r.Draft}: {r.Proposed} proposed"));
    }

    private static string SyncSummary(SyncContextSourcesResult result)
    {
        var failed = result.Sources.Count(static s => s.Links.Code is not null);
        var linked = result.Sources.Sum(static s => s.Links.Linked);
        return string.Create(CultureInfo.InvariantCulture, $"{result.Sources.Count} source(s) synced, {linked} linked{(failed > 0 ? $", {failed} with a code" : string.Empty)}");
    }

    /// <summary>A named source that can't be a source id is unknown (SND-CONTEXT-058) before any job exists.</summary>
    private static ValueTask<Result<Unit>> PrecheckSync(SyncContextSourcesRequest request, CancellationToken cancellationToken)
    {
        Result<Unit> checkedRequest = request.Source is null || SourceId.TryFrom(request.Source, out _)
            ? Unit.Value
            : ContextErrors.SyncSourceUnknown.With($"'{request.Source}' isn't a source id (kebab-case); fix: sindri context sources status");
        return ValueTask.FromResult(checkedRequest);
    }
}
