using Aw.Kernel.Services;
using Aw.Readiness.App.Features.CheckRepo;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Readiness.App.Features;

/// <summary>Registers Aw.Readiness.App.Features's slices: each service, its validator and its decorators (outermost first).</summary>
internal static class ReadinessFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Readiness use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddReadinessFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<CheckRepoService>()
            .AddScoped<IValidator<CheckRepoRequest>, CheckRepoValidator>()
            .AddScoped<IService<CheckRepoRequest, CheckRepoResponse>>(static sp =>
                new ValidatingService<CheckRepoRequest, CheckRepoResponse>(
                    sp.GetRequiredService<CheckRepoService>(),
                    sp.GetRequiredService<IValidator<CheckRepoRequest>>()));
        return services;
    }
}
