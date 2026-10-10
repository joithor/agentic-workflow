using Aw.Judge.Features.GetHealth;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Features;

/// <summary>Registers Aw.Judge.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class JudgeFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Judge use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<GetHealthService>()
            .AddScoped<IValidator<GetHealthRequest>, GetHealthValidator>()
            .AddScoped<IService<GetHealthRequest, GetHealthResponse>>(static sp =>
                new ValidatingService<GetHealthRequest, GetHealthResponse>(
                    sp.GetRequiredService<GetHealthService>(),
                    sp.GetRequiredService<IValidator<GetHealthRequest>>()));
        return services;
    }
}
