using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.CodeIndex.Features;

/// <summary>Registers Aw.CodeIndex.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class CodeIndexFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every CodeIndex use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCodeIndexFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<BuildCodeIndexService>()
            .AddScoped<IValidator<BuildCodeIndexRequest>, BuildCodeIndexValidator>()
            .AddScoped<IService<BuildCodeIndexRequest, BuildCodeIndexResponse>>(static sp =>
                new ValidatingService<BuildCodeIndexRequest, BuildCodeIndexResponse>(
                    sp.GetRequiredService<BuildCodeIndexService>(),
                    sp.GetRequiredService<IValidator<BuildCodeIndexRequest>>()));
        return services;
    }
}
