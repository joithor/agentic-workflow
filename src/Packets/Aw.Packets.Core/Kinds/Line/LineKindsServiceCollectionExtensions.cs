using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Registers the line kinds' services.</summary>
public static class LineKindsServiceCollectionExtensions
{
    /// <summary>Adds the line packet inspector.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLinePacketKinds(this IServiceCollection services) =>
        services.AddScoped<LinePacketInspector>();
}
