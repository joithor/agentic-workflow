using Aw.BugFix.Features.Init;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Features;

/// <summary>Registers Aw.BugFix.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class BugFixFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every BugFix use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBugFixFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<InitService>()
            .AddScoped<IValidator<InitRequest>, InitValidator>()
            .AddScoped<IService<InitRequest, InitResponse>>(static sp =>
                new ValidatingService<InitRequest, InitResponse>(
                    sp.GetRequiredService<InitService>(),
                    sp.GetRequiredService<IValidator<InitRequest>>()));
        return services;
    }
}
