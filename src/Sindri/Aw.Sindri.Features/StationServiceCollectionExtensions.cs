using Aw.Sindri.Features.Build;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Features.Evidence;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Features.Ship;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the block runtimes of the line's stations and of the evidence blocks.</summary>
internal static class StationServiceCollectionExtensions
{
    /// <summary>Adds every station slice's Step runtimes.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddStationSlices(this IServiceCollection services) =>
        services.AddScopingSlices().AddBuildSlices().AddShipSlices().AddLensLoops().AddProveSlices().AddEvidenceSlices().AddDocsSlices();
}
