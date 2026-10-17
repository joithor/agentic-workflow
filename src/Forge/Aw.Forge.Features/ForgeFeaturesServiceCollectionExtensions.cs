using Aw.Forge.Features.ParseForgeLink;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Features;

/// <summary>Registers Aw.Forge.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class ForgeFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Forge use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddForgeFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<ParseForgeLinkService>()
            .AddScoped<IValidator<ParseForgeLinkRequest>, ParseForgeLinkValidator>()
            .AddScoped<IService<ParseForgeLinkRequest, ParseForgeLinkResponse>>(static sp =>
                new ValidatingService<ParseForgeLinkRequest, ParseForgeLinkResponse>(
                    sp.GetRequiredService<ParseForgeLinkService>(),
                    sp.GetRequiredService<IValidator<ParseForgeLinkRequest>>()));
        return services;
    }
}
