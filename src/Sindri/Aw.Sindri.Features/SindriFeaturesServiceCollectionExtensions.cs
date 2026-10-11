using Aw.Kernel.Services;
using Aw.Sindri.Features.GetStatus;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers Aw.Sindri.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class SindriFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Sindri use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<GetStatusService>()
            .AddScoped<IValidator<GetStatusRequest>, GetStatusValidator>()
            .AddScoped<IService<GetStatusRequest, GetStatusResponse>>(static sp =>
                new ValidatingService<GetStatusRequest, GetStatusResponse>(
                    sp.GetRequiredService<GetStatusService>(),
                    sp.GetRequiredService<IValidator<GetStatusRequest>>()));
        return services;
    }
}
