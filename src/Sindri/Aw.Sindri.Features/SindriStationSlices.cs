using Aw.Sindri.Features.Build;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Features.Review;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Features.Ship;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the block runtimes of the line's stations (one slice each).</summary>
internal static class SindriStationSlices
{
    /// <summary>Adds the stations' Steps.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddStationSlices(this IServiceCollection services) =>
        services.AddScopingSlices().AddLineSlices().AddBuildSlices().AddShipSlices().AddLensLoops().AddProveSlices().AddReviewSlices();
}
