using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetEngine;
using Aw.Sindri.Features.GetEngineHistory;
using Aw.Sindri.Features.ListEngineInstances;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the engine in depth's three queries (API P13, UI E-30).</summary>
internal static class EngineServiceCollectionExtensions
{
    internal static IServiceCollection AddEngineReads(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<IValidator<GetEngineRequest>, AcceptAllValidator<GetEngineRequest>>().AddQuery<GetEngineRequest, EngineView, GetEngineService>()
            .AddScoped<IValidator<ListEngineInstancesRequest>, AcceptAllValidator<ListEngineInstancesRequest>>().AddQuery<ListEngineInstancesRequest, EngineInstanceList, ListEngineInstancesService>()
            .AddScoped<IValidator<GetEngineHistoryRequest>, AcceptAllValidator<GetEngineHistoryRequest>>().AddQuery<GetEngineHistoryRequest, EngineHistory, GetEngineHistoryService>();
    }
}
