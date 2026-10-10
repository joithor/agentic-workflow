using Aw.Evidence.Features.RunScript;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Features;

/// <summary>Registers Aw.Evidence.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class EvidenceFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Evidence use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEvidenceFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<RunScriptService>()
            .AddScoped<IValidator<RunScriptRequest>, RunScriptValidator>()
            .AddScoped<IService<RunScriptRequest, RunScriptResponse>>(static sp =>
                new ValidatingService<RunScriptRequest, RunScriptResponse>(
                    sp.GetRequiredService<RunScriptService>(),
                    sp.GetRequiredService<IValidator<RunScriptRequest>>()));
        return services;
    }
}
