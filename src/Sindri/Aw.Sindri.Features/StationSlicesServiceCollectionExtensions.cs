using Aw.Sindri.Features.Build;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Features.Ship;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the line's stations: scoping, design and plan, build, ship, the lens loops and prove.</summary>
internal static class StationSlicesServiceCollectionExtensions
{
    /// <summary>Adds every station's block runtimes and slices.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddStationSlices(this IServiceCollection services) =>
        services.AddScopingSlices().AddLineSlices().AddLineBlockRuntimes().AddBuildSlices().AddShipSlices().AddLensLoops().AddProveSlices();
}
