using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordServiceRun;
using Aw.Sindri.Features.RunService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features;

/// <summary>Registers the slices API P6 adds that the other slices share (the manual run of a service and the recorder of a service's run).</summary>
internal static class RunServiceServiceCollectionExtensions
{
    /// <summary>Adds the RunService job, keyed by the service and summarised by RunServiceSummary, its admission and the guard it shares with the scheduler.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRunService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ServiceRunGuard>();
        return services.AddScoped<IJobAdmission<RunServiceRequest>, RunServiceAdmission>()
            .AddJob<RunServiceRequest, ScheduledResult, RunServiceService>(static r => JobKey.From(r.Name.Value), static r => RunServiceSummary.Of(r));
    }

    /// <summary>Adds RecordServiceRun, the scheduler's command, and the port RunService records a manual run through.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecordServiceRun(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<RecordServiceRunService>()
            .AddScoped<Kernel.Services.IService<RecordServiceRunRequest, RecordServiceRunResponse>>(static sp => sp.GetRequiredService<RecordServiceRunService>())
            .AddScoped<IServiceRunRecorder>(static sp => sp.GetRequiredService<RecordServiceRunService>());
    }
}
