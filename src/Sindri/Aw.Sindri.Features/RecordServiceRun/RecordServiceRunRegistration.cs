using Aw.Kernel.Services;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.RecordServiceRun;

/// <summary>Registers RecordServiceRun both as the scheduler's command and as the port RunService records a manual run through.</summary>
internal static class RecordServiceRunRegistration
{
    /// <summary>Adds the scoped service under both of its interfaces.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecordServiceRun(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<RecordServiceRunService>()
            .AddScoped<IService<RecordServiceRunRequest, RecordServiceRunResponse>>(static sp => sp.GetRequiredService<RecordServiceRunService>())
            .AddScoped<IServiceRunRecorder>(static sp => sp.GetRequiredService<RecordServiceRunService>());
    }
}
