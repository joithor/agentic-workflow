using Aw.Judge.Features.GetBrief;
using Aw.Judge.Features.GetHealth;
using Aw.Judge.Features.MapBriefByName;
using Aw.Judge.Features.SaveBrief;
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
        _ = services
            .AddScoped<SaveBriefService>()
            .AddScoped<IValidator<SaveBriefRequest>, SaveBriefValidator>()
            .AddScoped<IService<SaveBriefRequest, SaveBriefResponse>>(static sp =>
                new ValidatingService<SaveBriefRequest, SaveBriefResponse>(
                    sp.GetRequiredService<SaveBriefService>(),
                    sp.GetRequiredService<IValidator<SaveBriefRequest>>()));
        _ = services
            .AddScoped<GetBriefService>()
            .AddScoped<IValidator<GetBriefRequest>, GetBriefValidator>()
            .AddScoped<IService<GetBriefRequest, GetBriefResponse>>(static sp =>
                new ValidatingService<GetBriefRequest, GetBriefResponse>(
                    sp.GetRequiredService<GetBriefService>(),
                    sp.GetRequiredService<IValidator<GetBriefRequest>>()));
        _ = services
            .AddScoped<MapBriefByNameService>()
            .AddScoped<IValidator<MapBriefByNameRequest>, MapBriefByNameValidator>()
            .AddScoped<IService<MapBriefByNameRequest, MapBriefByNameResponse>>(static sp =>
                new ValidatingService<MapBriefByNameRequest, MapBriefByNameResponse>(
                    sp.GetRequiredService<MapBriefByNameService>(),
                    sp.GetRequiredService<IValidator<MapBriefByNameRequest>>()));
        return services;
    }
}
