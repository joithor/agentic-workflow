using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Registers the scoping station's three services (scoping spec §8.2, §8.4).</summary>
internal static class ScopingServiceCollectionExtensions
{
    /// <summary>Adds 'scoping check', 'scoping show' and 'scoping desk' as scoped services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddScopingSlices(this IServiceCollection services) => services
        .AddScoped<IService<ScopingCheckRequest, ScopingCheckResponse>, ScopingCheckService>()
        .AddScoped<IService<ScopingShowRequest, ScopingView>, ScopingShowService>()
        .AddScoped<IService<ScopingDeskRequest, ScopingDeskResponse>, ScopingDeskService>();
}
