using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Sessions;

/// <summary>Registers the session collector's services (collector spec CI-4): the pass, the ledger append and the restart restore.</summary>
internal static class SessionsServiceCollectionExtensions
{
    /// <summary>Adds the collector's memory and helpers as singletons and its three services as scoped IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSessionSlices(this IServiceCollection services) => services
        .AddSingleton<CollectorMemory>()
        .AddSingleton<SessionRepos>()
        .AddSingleton<SessionDiscovery>()
        .AddSingleton<SessionObserver>()
        .AddScoped<IService<CollectSessionsRequest, CollectSessionsResponse>, CollectSessionsService>()
        .AddScoped<IService<RecordFleetChangesRequest, RecordFleetChangesResponse>, RecordFleetChangesService>()
        .AddScoped<IService<RestoreFleetHistoryRequest, RestoreFleetHistoryResponse>, RestoreFleetHistoryService>();
}
