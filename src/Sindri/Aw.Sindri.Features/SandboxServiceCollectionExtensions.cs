using Aw.Kernel.Services;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.PlanSandbox;
using Aw.Sindri.Features.RecordLimitHit;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the sandbox slices (sandbox spec §12, SB-1 onward).</summary>
internal static class SandboxServiceCollectionExtensions
{
    /// <summary>Adds PlanSandbox, the plan builder behind 'sindri sandbox show' and 'explain'.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPlanSandbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IService<PlanSandboxRequest, PlanSandboxResponse>, PlanSandboxService>();
    }

    /// <summary>Adds RecordLimitHit, the issue 'session wrap' makes when a limit is reached that does not end the session (proven by the launch secret).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecordLimitHit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IValidator<RecordLimitHitRequest>, AcceptAllValidator<RecordLimitHitRequest>>().AddIssue<RecordLimitHitRequest, RecordLimitHitResponse, RecordLimitHitService>();
    }
}
