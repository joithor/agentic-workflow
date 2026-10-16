using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Features.RunService;

/// <summary>Registers RunService as a job (API P6 unit D): keyed by the service, admitted inside the gate, summarised as 'ok: worked' or 'ok: idle'.</summary>
internal static class RunServiceRegistration
{
    /// <summary>Adds the RunService job and the guard it shares with the scheduler.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRunService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ServiceRunGuard>();
        return services.AddJob<RunServiceRequest, ScheduledResult, RunServiceService>(
            static r => JobKey.From(r.Name.Value), static r => r.Idle ? "ok: idle" : "ok: worked", RunServiceAdmission.Check);
    }
}
